/*==============================================================================
 * (C) Copyright 2017,2022 John J Kauflin, All rights reserved.
 *----------------------------------------------------------------------------
 * DESCRIPTION:  Server-side code to execute tasks and log text to a display
 *               using a websocket connection
 *----------------------------------------------------------------------------
 * Modification History
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
 *============================================================================*/
using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using FluentFTP;
using MySqlConnector;
using static System.Net.WebRequestMethods;

namespace MediaGalleryAdmin
{
    public class ExecuteTask
    {
        private static readonly Stopwatch timer = new Stopwatch();
        private static WebSocket? webSocket;
        private static string? dbConnStr;
        //private static System.Collections.Specialized.StringCollection log = new System.Collections.Specialized.StringCollection();
        private static DateTime lastRunDate;
        private static ArrayList fileList = new ArrayList();

        public ExecuteTask(WebSocket inWebSocket, string inDbConnStr)
        {
            webSocket = inWebSocket;
            dbConnStr = inDbConnStr;
        }

        public void FileTransfer()
        {
            try
            {
                timer.Start();
                log($"Beginning of FileTransfer");

                string ftpHost = GetConfigParamValue("FTP_HOST");
                string ftpUser = GetConfigParamValue("FTP_USER");
                string ftpPass = GetConfigParamValue("FTP_PASS");
                string webRoolUrl = GetConfigParamValue("WEB_ROOT_URL");
                string localPhotosRoot = GetConfigParamValue("LOCAL_PHOTOS_ROOT");
                string remotePhotosRoot = GetConfigParamValue("REMOTE_PHOTOS_ROOT");
                string photosStartDir = GetConfigParamValue("PHOTOS_START_DIR");

                lastRunDate = DateTime.Parse(GetConfigParamValue("LastRunDate"));
                log($"Last Run = {lastRunDate.ToString("MM/dd/yyyy HH:mm:ss")}");
                var startDateTime = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

                // Start the recursive function (which will only complete when all subsequent recursive calls are done)
                var root = new DirectoryInfo(localPhotosRoot + photosStartDir);
                fileList.Clear();
                WalkDirectoryTree(root);

                if (fileList.Count == 0)
                {
                    log("No new files found");
                    return;
                }

                DateTime remoteFileModifiedDateTime;
                bool fileExists;
                bool fileModified = false;
                bool dirExists;
                using (var ftpConn = new FtpClient(ftpHost, ftpUser, ftpPass))
                {
                    ftpConn.Config.EncryptionMode = FtpEncryptionMode.Explicit;
                    ftpConn.Config.ValidateAnyCertificate = true;
                    ftpConn.Connect();

                    if (!ftpConn.DirectoryExists(remotePhotosRoot))
                    {
                        log($"Remote FTP directory ROOT does not exist, dir = {remotePhotosRoot}");
                        return;
                    }

                    ftpConn.SetWorkingDirectory(remotePhotosRoot);
                    string fileNameAndPath;
                    string dirPath;
                    int index = 0;
                    foreach (FileInfo fi in fileList)
                    {
                        index++;
                        fileNameAndPath = fi.FullName.Substring(localPhotosRoot.Length);
                        dirPath = "";
                        if (!String.IsNullOrEmpty(fi.DirectoryName))
                        {
                            dirPath = fi.DirectoryName.Substring(localPhotosRoot.Length);
                        }

                        log($"{index} of {fileList.Count}, {fileNameAndPath}");

                        fileExists = false;
                        fileModified = false;
                        dirExists = false;
                        try
                        {
                            fileExists = ftpConn.FileExists(fileNameAndPath);
                            dirExists = true;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("No such file or directory"))
                            {
                                dirExists = false;
                            }
                            else
                            {
                                log(ex.Message);
                                throw;
                            }
                        }

                        if (!dirExists)
                        {
                            //log($"    Create Dir = {dirPath}");
                            ftpConn.CreateDirectory(dirPath, true);
                        }

                        if (fileExists)
                        {
                            remoteFileModifiedDateTime = ftpConn.GetModifiedTime(fileNameAndPath);
                            if (fi.LastWriteTime > remoteFileModifiedDateTime)
                            {
                                fileModified = true;
                            }
                            else
                            {
                                // compare the downloaded file with the server
                                if (ftpConn.CompareFile(fi.FullName, fileNameAndPath) != FtpCompareResult.Equal) 
                                {
                                    fileModified = true;
                                }
                            }
                        }

                        if (!fileExists || fileModified)
                        {
                            ftpConn.Config.RetryAttempts = 3;
                            ftpConn.UploadFile(fi.FullName, fileNameAndPath, FtpRemoteExists.Overwrite, false, FtpVerify.Retry);

                            // Create thumbnails
                        }
                    } // foreach (FileInfo fi in fileList)

                    ftpConn.Disconnect();
                } // using (var ftpConn = new FtpClient(ftpHost, ftpUser, ftpPass))

                // Update LastRunDate with the startDateTime from this run
                UpdConfigParamValue("LastRunDate", startDateTime);

                timer.Stop();
                log($"END of FileTransfer, elapsed time = {timer.Elapsed.ToString()}");
            }
            catch (Exception ex)
            {
                log($"*** Error occurred, message = {ex.Message}");
            }

        } // public void FileTransfer()


        private static void WalkDirectoryTree(DirectoryInfo root)
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

        private static void log(string dataStr)
        {
            if (webSocket != null)
            {
                var encoded = Encoding.UTF8.GetBytes(dataStr);
                var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
                webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }


}
