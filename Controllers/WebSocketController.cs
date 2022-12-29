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
 * 2022-12-23 JJK   Working on update file info (to new DB table)
 * 2022-12-24 JJK   Implemented a Dictionary to hold config keys and values
 * 2022-12-27 JJK   Implemented micro-ORM to do database work (see Model)
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
using MediaGalleryAdmin.Model;
using System.Reflection.PortableExecutable;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;

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

    private static Dictionary<string, string> configParamDict = new Dictionary<string, string>();

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
            webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            dbConnStr = _configuration["dbConnStr"];
            LoadConfigParam();
            if (configParamDict.Count == 0)
            {
                log("*** Problem loading configuration parameters - check logs and database");
                return;
            }

            if (taskName.Equals("FileTransfer"))
            {
                FileTransfer();
            } else if (taskName.Equals("UpdateFileInfo"))
            {
                UpdateFileInfo();
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
        Console.WriteLine(dataStr);
        _log.LogInformation(dataStr);
        if (webSocket != null)
        {
            var encoded = Encoding.UTF8.GetBytes(dataStr);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }


    private void UpdateFileInfo()
    {
        try
        {
            log($"Beginning of Update File Info");
            timer.Start();

            // re-write to get these all in the same transaction (maybe load into a dictionary)
            string localPhotosRoot = configParamDict["LOCAL_PHOTOS_ROOT"];
            string photosStartDir = configParamDict["PHOTOS_START_DIR"];

            //lastRunDate = DateTime.Parse(configParamDict["LastRunDate"]);
            // For TESTING
            lastRunDate = DateTime.Parse("01/01/2000");
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

            int index = 0;
            FileInfo fi;
            FileInfoTable fiRec;
            string fileNameAndPath;
            int pos;
            string tempStr;
            string category;
            DateTime taken;
            var mgr = new MediaGalleryRepository(null);
            while (index < fileList.Count)
            {
                fi = (FileInfo)fileList[index];
                log($"{index + 1} of {fileList.Count}, {fi.FullName}");

                fileNameAndPath = fi.FullName.Substring(localPhotosRoot.Length).Replace(@"\", @"/");
                pos = fileNameAndPath.IndexOf(@"/");
                tempStr = fileNameAndPath.Substring(pos+1);
                pos = tempStr.IndexOf(@"/");
                category = tempStr.Substring(0,pos);
                taken = DateTime.Now;
                
                using (var conn = new MySqlConnection(dbConnStr))
                {
                    conn.Open();
                    mgr.setConnection(conn);
                    fiRec = mgr.getFileInfoTable(fi.Name);
                    if (fiRec != null)
                    {
                        // Update
                        fiRec.Category = category;
                        fiRec.FullNameLocal = fi.FullName;
                        fiRec.NameAndPath = fileNameAndPath;
                        fiRec.CreateDateTime = fi.CreationTime;
                        fiRec.LastModified = fi.LastWriteTime;
                        //fiRec.TakenDateTime = taken;
                        //fiRec.Title = "Title";
                        //fiRec.Description = "Description";
                        //fiRec.People = "People";
                        fiRec.ToBeProcessed = 1;
                        mgr.updateFileInfoTable(fiRec);
                    }
                    else
                    {
                        // Insert
                        fiRec = new FileInfoTable();
                        fiRec.Name = fi.Name;
                        fiRec.Category = category;
                        fiRec.FullNameLocal = fi.FullName;
                        fiRec.NameAndPath = fileNameAndPath;
                        fiRec.CreateDateTime = fi.CreationTime;
                        fiRec.LastModified = fi.LastWriteTime;
                        fiRec.TakenDateTime = taken;
                        fiRec.Title = "Title";
                        fiRec.Description = "Description";
                        fiRec.People = "People";
                        fiRec.ToBeProcessed = 1;
                        mgr.insertFileInfoTable(fiRec);
                    }
                    
                    conn.Close();
                }
                
                // Increment to the next file if all operations were successful
                index++;
            } // Loop through the file list


            // Update LastRunDate with the startDateTime from this run
            UpdConfigParamValue("LastRunDate", startDateTime);

            timer.Stop();
            log($"END elapsed time = {timer.Elapsed.ToString()}");
        }
        catch (Exception ex)
        {
            log($"*** Error: {ex.Message}");
            log($"END elapsed time = {timer.Elapsed.ToString()}");
            throw;
        }
    } // private void updateFileInfo()


    public void FileTransfer()
    {
        try
        {
            log($"Beginning of FileTransfer");
            timer.Start();

            // Get config values from the static dictionary
            string ftpHost = configParamDict["FTP_HOST"];
            string ftpUser = configParamDict["FTP_USER"];
            string ftpPass = configParamDict["FTP_PASS"];
            string webRootUrl = configParamDict["WEB_ROOT_URL"];
            string localPhotosRoot = configParamDict["LOCAL_PHOTOS_ROOT"];
            string remotePhotosRoot = configParamDict["REMOTE_PHOTOS_ROOT"];
            string photosStartDir = configParamDict["PHOTOS_START_DIR"];

            int maxRows = 100;
            int index = 0;
            int index2 = 0;
            int tempCnt = 0;
            int retryCnt = 0;
            int maxRetry = 3;
            var mgr = new MediaGalleryRepository(null);
            List<FileInfoTable> fileInfoTableList;
            FileInfoTable fiRec;
            bool done = false;
            bool done2 = false;
            string createThumbnailUrl;
            while (!done)
            {
                retryCnt++;
                if (retryCnt > maxRetry & tempCnt == 0)
                {
                    // If no records have been processed in the last X number of re-tries, exit out
                    done = true;
                    log("*** Max re-tries exceeded with no records processed - exiting");
                    continue;
                }

                tempCnt = 0;

                // Get a set of files to process from the database
                using (var conn = new MySqlConnection(dbConnStr))
                {
                    conn.Open();
                    mgr.setConnection(conn);
                    fileInfoTableList = mgr.getFileInfoTableList(maxRows);
                    if (fileInfoTableList == null)
                    {
                        log("No files to be processed");
                        done = true;
                        continue;
                    }
                    if (fileInfoTableList.Count == 0)
                    {
                        log("No files to be processed");
                        done = true;
                        continue;
                    }
                }

                // Open an FTP connection for the file transfer
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
                        done = true;
                        continue;
                    }

                    ftpConn.SetWorkingDirectory(remotePhotosRoot);
                    done2 = false;
                    index2 = 0;
                    while (index2 < fileInfoTableList.Count && !done2)
                    //foreach (var fiRec in fileInfoTableList)
                    {
                        fiRec = (FileInfoTable)fileInfoTableList[index2];
                        log($"{index + 1}, {index2 + 1}, {fiRec.NameAndPath}");

                        try
                        {
                            // Try the FTP upload, overwriting existing files, and "true" to create directories if they do not exist
                            // Not checking modified dates on the FTP file because we count on the local to show which are changed and don't want to make an extra FTP call
                            ftpConn.Config.RetryAttempts = 3;
                            ftpConn.UploadFile(fiRec.FullNameLocal, fiRec.NameAndPath, FtpRemoteExists.Overwrite, true, FtpVerify.Retry);

                            // Make an HTTPS GET call to create the thumbnail and smaller files
                            createThumbnailUrl = webRootUrl + "/vendor/jkauflin/jjkgallery/createThumbnail.php?filePath=" + fiRec.NameAndPath;
                            GetAsync(createThumbnailUrl).Wait();

                            // Update the flag in the db for the file (to indicate processing is done)
                            using (var conn = new MySqlConnection(dbConnStr))
                            {
                                conn.Open();
                                mgr.setConnection(conn);
                                mgr.updFileInfoToBeProcessed(fiRec.Name, false);
                            }

                            // Increment to the next file if all operations were successful
                            index2++;
                            index++;
                            tempCnt++;
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Error in file processing");
                            string exMessage = ex.Message;
                            if (ex.InnerException != null)
                            {
                                exMessage = exMessage + " " + ex.InnerException.Message;
                            }
                            log($">>> Error: {exMessage}");

                            if (exMessage.Contains("No such file or directory"))
                            {
                                int pos = fiRec.NameAndPath.LastIndexOf(@"/");
                                string dirPath = fiRec.NameAndPath.Substring(0, pos);
                                log($">>> Create missing Dir = {dirPath}");
                                ftpConn.CreateDirectory(dirPath, true);
                            }

                            // Disconnect the FTP, drop out of the inner loop and let it start again
                            ftpConn.Disconnect();
                            done2 = true;
                            // Wait for X seconds to give connection issues a chance to resolve
                            Thread.Sleep(8000);
                            continue;
                        }
                    }

                } // using (var ftpConn = new FtpClient(ftpHost, ftpUser, ftpPass))

            } // Loop through the file list


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


    // Load all the keys and values from the database Config table into a static Dictionary
    private static void LoadConfigParam()
    {
        using (var conn = new MySqlConnection(dbConnStr))
        {
            conn.Open();
            var mgr = new MediaGalleryRepository(conn);
            var configParamList = mgr.getConfigParam();
            configParamDict.Clear();
            foreach (var configParam in configParamList)
            {
                configParamDict.Add(configParam.ConfigName, configParam.ConfigValue);
            }
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
                var mgr = new MediaGalleryRepository(conn);
                mgr.UpdateConfigParamValue(configName, configValue);    
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
