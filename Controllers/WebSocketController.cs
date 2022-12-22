/*==============================================================================
 * (C) Copyright 2017,2022 John J Kauflin, All rights reserved.
 *----------------------------------------------------------------------------
 * DESCRIPTION:  Server-side Controller to handle websocket requests with
 *               a specified task to execute
 *               Server-side code to execute tasks and log text to a display
 *               using a websocket connection
 *----------------------------------------------------------------------------
 * Modification History
 * 2017-09-08 JJK 	Initial version
 * 2017-12-29 JJK	Initial controls and WebSocket communication
 * 2022-04-17 JJK   Making updates for bootstrap 5, and to use fetch()
 *                  instead of AJAX.  Removed old websocket stuff
 * 2022-05-31 JJK   Updated to use newest fetch ideas for lookup and update,
 *                  and converted to use straight javascript
 * 2022-10-20 JJK   Re-implemented websocket connection to display async log
 * 2022-12-17 JJK   Re-implemented using .NET 6 C# backend server instead of
 *                  nodejs.  Got User Secrets, Configuration injection, 
 *                  and connection string for remote MySQL working
 * 2022-12-20 JJK   Implemented NLog logger and added to dependency injection
 *----------------------------------------------------------------------------
 * 2019-02-11 JJK   Initial version
 * 2020-07-07 JJK   Modified to work with new MediaGallery and createThumbnails which takes "subPath" as a parameter
 * 2021-05-09 JJK   Re-factored for MediaGallery-Admin. Working on FTP functions
 * 2021-05-27 JJK   Re-worked the file loop to get list of only image files
 * 2021-05-28 JJK   Completed new FTP and create thumbnail logic
 * 2021-07-03 JJK   Added logic to create the remote directory if missing
 * 2021-10-30 JJK   Modified to save a last completed timestamp and look for files with a timestamp greater than last run
 * 2022-10-20 JJK   Re-implemented websocket connection to display async log
 * 2022-12-17 JJK   Re-implemented using .NET 6 C# backend server instead of nodejs
 * 2022-12-18 JJK   (Argentina beats France to win world cup)  Implemented
 *                  recursive walk through of directories and verified the
 *                  recursive "queue" completes before the first call returns
 *                  (unlike nodejs)
 * 2022-12-19 JJK   Got MySQL queries to work on ConfigParam
 * 2022-12-20 JJK   Got FTP functions, and LastRunDate parameter update working
 * 2022-12-21 JJK   Got https GET working for CreateThumbnail call
 * 2022-12-22 JJK   Moved the execution tasks into the Controller (to take 
 *                  advantage of the injected logger)
 *============================================================================*/
using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using FluentFTP;
using MySqlConnector;
using static System.Net.WebRequestMethods;


namespace MediaGalleryAdmin.Controllers;

public class WebSocketController : ControllerBase
{
    public IConfiguration _configuration;
    private readonly ILogger<WebSocketController> _log;

    private static readonly Stopwatch timer = new Stopwatch();
    private static WebSocket? webSocket;
    private static string? dbConnStr;
    //private static System.Collections.Specialized.StringCollection log = new System.Collections.Specialized.StringCollection();
    private static DateTime lastRunDate;
    private static ArrayList fileList = new ArrayList();
    private static HttpClient httpClient = new HttpClient();

    public WebSocketController (IConfiguration configuration, ILogger<WebSocketController> logger)
    {
        _configuration = configuration;
        _log = logger;
        _log.LogDebug(1, "NLog injected into Controller");
    }

    [HttpGet("/ws")]
    public async Task Get(string taskName)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            _log.LogInformation(">>> Accepting WebSocket request");
            //using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            dbConnStr = _configuration["dbConnStr"];

            // Instantiate object to execute tasks passing it a websocket object, and the DB connection string
            //ExecuteTask executeTask = new ExecuteTask(webSocket, _configuration["dbConnStr"]);

            if (taskName.Equals("FileTransfer"))
            {
                FileTransfer();
            }

            // Close the websocket after completion
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private void log(string dataStr)
    {
        _log.LogInformation(dataStr);

        if (webSocket != null)
        {
            var encoded = Encoding.UTF8.GetBytes(dataStr);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }


    public void FileTransfer()
    {
        try
        {
            log($"Beginning of FileTransfer");
            timer.Start();

            // re-write to get these all in the same transaction (maybe load into a dictionary)
            string ftpHost = GetConfigParamValue("FTP_HOST");
            string ftpUser = GetConfigParamValue("FTP_USER");
            string ftpPass = GetConfigParamValue("FTP_PASS");
            string webRoolUrl = GetConfigParamValue("WEB_ROOT_URL");
            string localPhotosRoot = GetConfigParamValue("LOCAL_PHOTOS_ROOT");
            string remotePhotosRoot = GetConfigParamValue("REMOTE_PHOTOS_ROOT");
            string photosStartDir = GetConfigParamValue("PHOTOS_START_DIR");

            /* This doesn' seem to work - had to use full URL in call
            httpClient = new()
            {
                BaseAddress = new Uri(GetConfigParamValue("WEB_ROOT_URL"))
            };
            */

            lastRunDate = DateTime.Parse(GetConfigParamValue("LastRunDate"));
            // For TESTING
            //lastRunDate = DateTime.Parse("01/01/2000");
            log($"Last Run = {lastRunDate.ToString("MM/dd/yyyy HH:mm:ss")}");
            var startDateTime = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

            var root = new DirectoryInfo(localPhotosRoot + photosStartDir);
            fileList.Clear();
            // Start the recursive function (which will only complete when all subsequent recursive calls are done)
            WalkDirectoryTree(root);

            if (fileList.Count == 0)
            {
                log("No new files found");
                return;
            }

            //bool fileExists;
            //bool fileModified = false;
            //bool dirExists;
            //DateTime remoteFileModifiedDateTime;
            using (var ftpConn = new FtpClient(ftpHost, ftpUser, ftpPass))
            {
                ftpConn.Config.EncryptionMode = FtpEncryptionMode.Explicit;
                ftpConn.Config.ValidateAnyCertificate = true;
                // For debugging
                //ftpConn.Config.LogToConsole = true;
                ftpConn.Connect();

                if (!ftpConn.DirectoryExists(remotePhotosRoot))
                {
                    log($"Remote FTP directory ROOT does not exist, dir = {remotePhotosRoot}");
                    return;
                }

                ftpConn.SetWorkingDirectory(remotePhotosRoot);
                string fileNameAndPath;
                string dirPath;
                string createThumbnailUrl;
                int index = 0;
                FileInfo fi;
                //foreach (FileInfo fi in fileList)
                while (index < fileList.Count)
                {
                    fi = (FileInfo)fileList[index];
                    fileNameAndPath = fi.FullName.Substring(localPhotosRoot.Length).Replace(@"\", @"/");
                    dirPath = "";
                    if (!String.IsNullOrEmpty(fi.DirectoryName))
                    {
                        dirPath = fi.DirectoryName.Substring(localPhotosRoot.Length);
                    }
                    log($"{index + 1} of {fileList.Count}, {fileNameAndPath}");

                    try
                    {
                        // Try the FTP upload, overwriting existing files, and "true" to create directories if they do not exist
                        // Not checking modified dates on the FTP file because we count on the local to show which are changed and don't want to make an extra FTP call
                        ftpConn.Config.RetryAttempts = 3;
                        ftpConn.UploadFile(fi.FullName, fileNameAndPath, FtpRemoteExists.Overwrite, true, FtpVerify.Retry);

                        // Make an HTTPS GET call to create the thumbnail and smaller files
                        createThumbnailUrl = GetConfigParamValue("WEB_ROOT_URL") + "/vendor/jkauflin/jjkgallery/createThumbnail.php?filePath=" + fileNameAndPath;
                        GetAsync(createThumbnailUrl).Wait();
                        // Increment to the next file if all operations were successful
                        index++;
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex,"Error in file processing");

                        //InnerException = Code: 550 Message: Photos/1 John J Kauflin/1987-to-1993/1989/1989-10 007.jpg: Temporary hidden file /Photos/1 John J Kauflin/1987-to-1993/1989/.in.1989-10 007.jpg. already exists
                        if (ex.Message.Contains("No such file or directory") || ex.Message.Contains("Temporary hidden file"))
                        {
                            // If it is a recoverable error, Disconnect, wait a few seconds, Re-connect and continue to the top of the loop to process the same file
                            log("*** Error - reconnecting...");
                            ftpConn.Disconnect();
                            Thread.Sleep(3000);
                            ftpConn.Connect();
                            ftpConn.SetWorkingDirectory(remotePhotosRoot);
                            continue;
                        }

                        // If unknown error, log and throw to exit
                        log($"FTP Error: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            log($"    InnerException = {ex.InnerException.Message}");
                        }
                        throw;
                    }

                } // Loop through the file list

                ftpConn.Disconnect();
            } // using (var ftpConn = new FtpClient(ftpHost, ftpUser, ftpPass))

            // Update LastRunDate with the startDateTime from this run
            UpdConfigParamValue("LastRunDate", startDateTime);

            timer.Stop();
            log($"END of FileTransfer, elapsed time = {timer.Elapsed.ToString()}");
        }
        catch (Exception ex)
        {
            log($"*** Error: {ex.Message}");
            log($"END of FileTransfer, elapsed time = {timer.Elapsed.ToString()}");
            throw;
        }

    } // public void FileTransfer()


    static async Task GetAsync(string urlParam)
    {
        //log($"urlParam = {urlParam}");
        using HttpResponseMessage response = await httpClient.GetAsync(urlParam);
        response.EnsureSuccessStatusCode();
        //var jsonResponse = await response.Content.ReadAsStringAsync();
    }

    private void WalkDirectoryTree(DirectoryInfo root)
    {
        FileInfo[] files = null;
        DirectoryInfo[] subDirs = null;

        // First, process all the files directly under this folder
        try
        {
            files = root.GetFiles("*.*");
        }
        // This is thrown if even one of the files requires permissions greater
        // than the application provides.
        catch (UnauthorizedAccessException e)
        {
            // This code just writes out the message and continues to recurse.
            // You may decide to do something different here. For example, you
            // can try to elevate your privileges and access the file again.
            log(e.Message);
        }
        catch (DirectoryNotFoundException e)
        {
            log(e.Message);
        }


        if (files != null)
        {
            string ext;
            foreach (FileInfo fi in files)
            {
                ext = fi.Extension.ToUpper();
                // Add only supported file types to the list
                if (ext.Equals(".JPEG") || ext.Equals(".JPG") || ext.Equals(".PNG") || ext.Equals(".GIF"))
                {
                    if (fi.LastWriteTime > lastRunDate)
                    {
                        //log($"Adding file = {fi.Name}");
                        //fileCnt++;
                        fileList.Add(fi);
                    }
                }
            } // foreach (FileInfo fi in files)

            // Now find all the subdirectories under this directory.
            subDirs = root.GetDirectories();

            foreach (DirectoryInfo dirInfo in subDirs)
            {
                // Resursive call for each subdirectory.
                WalkDirectoryTree(dirInfo);
            }
        }
    }

    private string GetConfigParamValue(string configParamName)
    {
        string strData = "";

        try
        {
            if (string.IsNullOrEmpty(configParamName))
            {
                return string.Empty;
            }

            using (var conn = new MySqlConnection(dbConnStr))
            {
                conn.Open();

                using var command = new MySqlCommand("SELECT ConfigValue FROM ConfigParam WHERE ConfigName = @ConfigName", conn);
                command.Parameters.AddWithValue("@ConfigName", configParamName);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    strData = reader.GetString(0);
                }

                conn.Close();
            }
            return strData;
        }
        catch (MySqlException ex)
        {
            log(ex.Message);
            return string.Empty;
        }
        catch (Exception ex)
        {
            log(ex.Message);
            return string.Empty;
        }
    }

    private void UpdConfigParamValue(string configName, string configValue)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
            {
                return;
            }

            using (var conn = new MySqlConnection(dbConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "UPDATE ConfigParam SET ConfigValue = @ConfigValue WHERE ConfigName = @ConfigName;  ";
                    cmd.Parameters.AddWithValue("ConfigValue", configValue);
                    cmd.Parameters.AddWithValue("ConfigName", configName);
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
            return;
        }
        catch (MySqlException ex)
        {
            log(ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            log(ex.Message);
            throw;
        }
    }


} // public class WebSocketController : ControllerBase
