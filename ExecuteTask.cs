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
 * 2022-12-17 JJK   Re-implemented using .NET 6 C# backend server instead ofnodejs
 *============================================================================*/
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;

using MySqlConnector;

namespace MediaGalleryAdmin
{
    public class ExecuteTask
    {
        private static WebSocket? webSocket;
        private static string? dbConnStr;

        public ExecuteTask(WebSocket inWebSocket, string inDbConnStr)
        {
            webSocket = inWebSocket;
            dbConnStr = inDbConnStr;
        }

        public void FileTransfer()
        {
            log($"Beginning task FileTransfer");

            // Need ENVIRONMENT variables for file path and FTP credentials (and DB credentials?)
            // *** maybe store task parameters in the DB, and show them in the web screen (for adjustment) before the run

            /*
            fs.readFile(lastRunFilename, function(err, buf) {
                if (!err)
                {
                    lastRunTimestamp = new Date(buf.toString());
                }
                log("Last Run Timestamp = " + lastRunTimestamp);

                if (process.env.LAST_RUN_TIMESTAMP_OVERRIDE != undefined)
                {
                    log("LAST_RUN_TIMESTAMP_OVERRIDE = " + process.env.LAST_RUN_TIMESTAMP_OVERRIDE);
                    lastRunTimestamp = new Date(process.env.LAST_RUN_TIMESTAMP_OVERRIDE);
                    log("Last Run Timestamp = " + lastRunTimestamp);
                }
                // Start the walkSync to load all the files into the filelist array

                fileList = walkSync(process.env.LOCAL_PHOTOS_ROOT + process.env.PHOTOS_START_DIR);
                //for (var i = 0, len = fileList.length; i < len; i++) {
                //    console.log("fileList[" + i + "] = " + fileList[i]);
                //}
                if (fileList.length > 0)
                {
                    startTransfer();
                }
                else
                {
                    log("No new pictures found")
                    log("")
                }
            });
            */



            // List all files in a directory in Node.js recursively in a synchronous fashion
            /*
            var filepath = '';
            var extension = '';
            var fileInfo = null;
            const lastRunFilename = 'lastRunTimestamp.log';
            var lastRunTimestamp = new Date('May 27, 95 00:00:00 GMT-0400');
            var fileList = null;
            */



            /*
            files = fs.readdirSync(dir,['utf8', 'true']);
            filelist = filelist || [];
            files.forEach(function(file) {
                filepath = dir + '/' + file;
                fileInfo = fs.statSync(filepath);

                if (fileInfo.isDirectory())
                {
                    filelist = walkSync(filepath, filelist);
                }
                else
                {
                    // Only add support file types to the list
                    extension = file.substring(file.lastIndexOf(".") + 1).toUpperCase();
                    if (extension == "JPEG" || extension == "JPG" || extension == "PNG" || extension == "GIF")

                        // File Last Modified
                        // fileInfo.mtime = Sat Oct 30 2021 09:50:11 GMT-0400 (Eastern Daylight Time)
                        //console.log(filepath);
                        //console.log("fileInfo.mtime = "+fileInfo.mtime+", "+lastRunTimestamp);
                        //console.log("fileInfo.ctime = "+fileInfo.ctime);
                        //console.log("fileInfo.atime = "+fileInfo.atime);

                        // Add to the list if the Created or Modified time is greater than the last run time
                        if (fileInfo.ctime.getTime() > lastRunTimestamp.getTime() ||
                            fileInfo.mtime.getTime() > lastRunTimestamp.getTime())
                        {
                            // Add the path minus the LOCAL ROOT
                            //console.log("Adding file = "+file);
                            filelist.push(dir.replace(process.env.LOCAL_PHOTOS_ROOT, '') + '/' + file);
                        }
                    }
                }
            });
            return filelist;
            */


            using (var mysqlconnection = new MySqlConnection(dbConnStr))
            {
                mysqlconnection.Open();
            }


            log("END of task");
        } // public void FileTransfer()


        private string GetValueFromDBUsing(string strQuery)
        {
            string strData = "";

            try
            {
                if (string.IsNullOrEmpty(strQuery) == true)
                    return string.Empty;

                using (var mysqlconnection = new MySqlConnection("Server=myserver;User ID=mylogin;Password=mypass;Database=mydatabase"))
                {
                    mysqlconnection.Open();
                    using (MySqlCommand cmd = mysqlconnection.CreateCommand())
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandTimeout = 300;
                        cmd.CommandText = strQuery;

                        object objValue = cmd.ExecuteScalar();
                        if (objValue == null)
                        {
                            cmd.Dispose();
                            return string.Empty;
                        }
                        else
                        {
                            strData = (string)cmd.ExecuteScalar();
                            cmd.Dispose();
                        }

                        mysqlconnection.Close();

                        if (strData == null)
                            return string.Empty;
                        else
                            return strData;
                    }
                }
            }
            catch (MySqlException ex)
            {
                //LogException(ex);
                return string.Empty;
            }
            catch (Exception ex)
            {
                //LogException(ex);
                return string.Empty;
            }
            finally
            {

            }
        }

        public class RecursiveFileSearch
        {
            static System.Collections.Specialized.StringCollection log = new System.Collections.Specialized.StringCollection();

            static void Main()
            {
                // Start with drives if you have to search the entire computer.
                string[] drives = System.Environment.GetLogicalDrives();

                foreach (string dr in drives)
                {
                    System.IO.DriveInfo di = new System.IO.DriveInfo(dr);

                    // Here we skip the drive if it is not ready to be read. This
                    // is not necessarily the appropriate action in all scenarios.
                    if (!di.IsReady)
                    {
                        Console.WriteLine("The drive {0} could not be read", di.Name);
                        continue;
                    }
                    System.IO.DirectoryInfo rootDir = di.RootDirectory;
                    WalkDirectoryTree(rootDir);
                }

                // Write out all the files that could not be processed.
                Console.WriteLine("Files with restricted access:");
                foreach (string s in log)
                {
                    Console.WriteLine(s);
                }
                // Keep the console window open in debug mode.
                Console.WriteLine("Press any key");
                Console.ReadKey();
            }

            static void WalkDirectoryTree(System.IO.DirectoryInfo root)
            {
                System.IO.FileInfo[] files = null;
                System.IO.DirectoryInfo[] subDirs = null;

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
                    log.Add(e.Message);
                }

                catch (System.IO.DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                }

                if (files != null)
                {
                    foreach (System.IO.FileInfo fi in files)
                    {
                        // In this example, we only access the existing FileInfo object. If we
                        // want to open, delete or modify the file, then
                        // a try-catch block is required here to handle the case
                        // where the file has been deleted since the call to TraverseTree().
                        Console.WriteLine(fi.FullName);
                    }

                    // Now find all the subdirectories under this directory.
                    subDirs = root.GetDirectories();

                    foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                    {
                        // Resursive call for each subdirectory.
                        WalkDirectoryTree(dirInfo);
                    }
                }
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
