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
 * 2022-12-29 JJK   Implemented final max retry checks
 * 2023-01-01 JJK   Implemented MetadataExtractor to data from photos, and
 *                  a special binary read to get the Picasa face people string
 * 2023-01-06 JJK   Implemented RegEx pattern to get DateTime from filename
 * 2023-01-11 JJK   Implemented ExifLibrary to get and set metadata values
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
/*
using System.Reflection.PortableExecutable;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Microsoft.VisualBasic;
*/
using System.Text.RegularExpressions;
//using Microsoft.Extensions.FileSystemGlobbing;
using ExifLibrary;

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
    private static List<DatePattern> dpList = new List<DatePattern>();

    public WebSocketController (IConfiguration configuration, ILogger<WebSocketController> logger)
    {
        _configuration = configuration;
        _log = logger;
        _log.LogDebug(1, "NLog injected into Controller");

        // Load the patterns to use for RegEx and DateTime Parse
        DatePattern datePattern;

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}-((0[1-9])|(1[012]))_(19|20)\d{2}((0[1-9])|(1[012]))((0[1-9]|[12]\d)|3[01])");
        datePattern.dateParseFormat = "yyyy-MM_yyyyMMdd";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"IMG_(19|20)\d{2}((0[1-9])|(1[012]))((0[1-9]|[12]\d)|3[01])");
        datePattern.dateParseFormat = "IMG_yyyyMMdd";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}((0[1-9])|(1[012]))((0[1-9]|[12]\d)|3[01])_\d{9}_iOS");
        datePattern.dateParseFormat = "yyyyMMdd_iOS";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}((0[1-9])|(1[012]))((0[1-9]|[12]\d)|3[01])");
        datePattern.dateParseFormat = "yyyyMMdd";
        dpList.Add(datePattern);
        // \d{4} to (19|20)\d{2}


        //+		fi	{D:\Photos\1 John J Kauflin\2016-to-2022\2018\01 Winter\FB_IMG_1520381172965.jpg}	System.IO.FileInfo



        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}-((0[1-9])|(1[012]))-((0[1-9]|[12]\d)|3[01])");
        datePattern.dateParseFormat = "yyyy-MM-dd";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}_((0[1-9])|(1[012]))_((0[1-9]|[12]\d)|3[01])");
        datePattern.dateParseFormat = "yyyy_MM_dd";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}-((0[1-9])|(1[012]))");
        datePattern.dateParseFormat = "yyyy-MM";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}_((0[1-9])|(1[012]))");
        datePattern.dateParseFormat = "yyyy_MM";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(19|20)\d{2}((0[1-9])|(1[012]))");
        datePattern.dateParseFormat = "yyyyMM";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"\\(19|20)\d{2}(\-|\ )");
        datePattern.dateParseFormat = "yyyy";
        dpList.Add(datePattern);

        datePattern = new DatePattern();
        datePattern.regex = new Regex(@"(\(|\\)(19|20)\d{2}(\)|\\)");
        datePattern.dateParseFormat = "yyyy";
        dpList.Add(datePattern);
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

            // >>>>>>>>>>>>>>>>>>>> is there a way to implement a STOP ?????????????????????????????

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

    private string getTagContent(FileInfo fi, string tagStartStr, string tagEndStr)
    {
        string contentStr = "";

        byte[] fileContent = null;
        System.IO.FileStream fs = new System.IO.FileStream(fi.FullName, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(fs);
        long byteLength = fi.Length;
        fileContent = binaryReader.ReadBytes((Int32)byteLength);
        fs.Close();
        fs.Dispose();
        binaryReader.Close();

        StringBuilder tempStr = new StringBuilder();
        char[] matchChars = tagStartStr.ToCharArray();
        bool matchStarted = false;
        int numCharsMatched = 0;
        bool tagFound = false;
        int maxContentStrLength = 1000;

        Int32 tempInt;
        bool done = false;
        for (int i = 0; i < byteLength && !done; i++)
        {
            tempInt = (Int32)fileContent[i];
            if (tempInt < 32 && tempInt > 126)
            {
                continue;
            }

            if (!tagFound)
            {
                if ((char)tempInt == matchChars[numCharsMatched])
                {
                    matchStarted = true;
                    numCharsMatched++;
                }
                else
                {
                    matchStarted = false;
                    numCharsMatched = 0;
                }

                if (matchStarted && numCharsMatched == matchChars.Length)
                {
                    tagFound = true;
                }
            }
            else
            {
                // Start collecting characters into a string, and check the string for end tag
                tempStr.Append((char)tempInt);
                if (tempStr.ToString().Contains(tagEndStr))
                {
                    int pos = tempStr.ToString().IndexOf(tagEndStr);
                    contentStr = tempStr.ToString().Substring(0, pos);
                    done = true;
                } 
                else
                {
                    if (tempStr.ToString().Length > maxContentStrLength)
                    {
                        contentStr = tempStr.ToString().Substring(0, 1000);
                        done = true;
                    }
                }
            }
        }

        //Console.Write($"contentStr = {contentStr}");
        return contentStr;
    }

    private DateTime getDateFromFilename(string fileName)
    {
        DateTime outDateTime = new DateTime(9999, 1, 1);
        string dateFormat;
        string dateStr;

        if (fileName.Contains("FB_IMG_"))
        {
            return outDateTime;
        }

        MatchCollection matches;
        bool found = false;
        int index = 0;
        // Loop through the defined RegEx patterns for date, find matches in the filename, and parse to get DateTime
        while (index < dpList.Count && !found)
        {
            matches = dpList[index].regex.Matches(fileName);
            if (matches.Count > 0)
            {
                found = true;
                // If there are multiple matches, just take the last one
                dateStr = matches[matches.Count-1].Value;
                dateFormat = dpList[index].dateParseFormat;

                // For this combined case, get the year-month from the start
                if (dateFormat.Equals("yyyy-MM_yyyyMMdd"))
                {
                    dateStr = dateStr.Substring(0, 7);
                    dateFormat = "yyyy-MM";
                }

                if (dateFormat.Equals("yyyyMMdd_iOS"))
                {
                    dateStr = dateStr.Substring(0, 8);
                    dateFormat = "yyyyMMdd";
                }

                if (dateFormat.Equals("IMG_yyyyMMdd"))
                {
                    dateStr = dateStr.Substring(4, 8);
                    dateFormat = "yyyyMMdd";
                }

                if (dateFormat.Equals("yyyy"))
                {
                    // Strip off the beginning and ending characters ("\" or "(") form the year match
                    dateStr = dateStr.Substring(1, 4);

                    // Check for a season tag and add a month to the year
                    if (fileName.Contains(" Winter"))
                    {
                        dateFormat = "yyyy-MM";
                        if (fileName.Contains("01 Winter")) {
                            dateStr = dateStr + "-01";
                        }
                        else
                        {
                            dateStr = dateStr + "-11";
                        }
                    }
                    else if (fileName.Contains(" Spring"))
                    {
                        dateFormat = "yyyy-MM";
                        dateStr = dateStr + "-04";
                    }
                    else if (fileName.Contains(" Summer"))
                    {
                        dateFormat = "yyyy-MM";
                        dateStr = dateStr + "-07";
                    }
                    else if (fileName.Contains(" Fall"))
                    {
                        dateFormat = "yyyy-MM";
                        dateStr = dateStr + "-09";
                    }
                }

                // D:\Photos\1 John J Kauflin\2009-to-2015\2013\04 Fall\Maria 459.jpg, date: \2013\, format: yyyy, *** PARSE FAILED *** 
                // *** check the EXIF info on this one ??????????????

                // >>>>> when querying a set of photos - make sure to include Filename as SORT ORDER
                // >>>>> something to search for duplicates?  (get rid of John 50th)

                // handling people names in the filename (just make sure included on search?  try to get into people string?

                if (DateTime.TryParseExact(dateStr, dateFormat, null, System.Globalization.DateTimeStyles.None, out outDateTime))
                {
                    //log($"{fileName}, date: {dateStr}, format: {dateFormat}, DateTime: {outDateTime}");
                }
                else
                {
                    log($"{fileName}, date: {dateStr}, format: {dateFormat}, *** PARSE FAILED ***");
                }
            }

            index++;
        }

        return outDateTime;
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

            lastRunDate = DateTime.Parse(configParamDict["LastRunDate"]);
            
            // For TESTING
            lastRunDate = DateTime.Parse("01/10/2023");
            photosStartDir = "Photos/1 John J Kauflin/2023-to-2029/2023/01 Winter";

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
            string peopleStr;
            var mgr = new MediaGalleryRepository(null);
            DateTime nullDate = new DateTime(0001, 1, 1);
            DateTime maxDateTime = new DateTime(9999, 1, 1);

            //IEnumerable<Directory> directories;
            //Directory subIfdDirectory;

            //DateTime exifDateTime;
            string exifDateTimeFormat = "yyyy:MM:dd HH:mm:ss";
            string tStr;

            while (index < fileList.Count && index < 2)
            {
                fi = (FileInfo)fileList[index];
                //_log.LogInformation($"{index + 1} of {fileList.Count}, {fi.FullName}");
                log($"{index + 1} of {fileList.Count}, {fi.FullName}");

                // Skip files in this directory
                if (fi.FullName.Contains(".picasaoriginals"))
                {
                    index++;
                    continue;
                }

                fileNameAndPath = fi.FullName.Substring(localPhotosRoot.Length).Replace(@"\", @"/");
                pos = fileNameAndPath.IndexOf(@"/");
                tempStr = fileNameAndPath.Substring(pos+1);
                pos = tempStr.IndexOf(@"/");
                category = tempStr.Substring(0,pos);

                // Get the photo date taken from the file name
                taken = getDateFromFilename(fi.FullName);

                //-----------------------------------------------------------------------------------------------------------------
                // Get the metadata from the photo files
                //-----------------------------------------------------------------------------------------------------------------
                var file = ImageFile.FromFile(fi.FullName);
                //foreach (var property in file.Properties)
                //{
                //    log($"  IFD:{property.IFD} Tag:{property.Tag} Name:{property.Name} Value:{property.Value} ");
                //}

                var exifArtist = file.Properties.Get<ExifAscii>(ExifTag.Artist);
                var exifCameraOwnerName = file.Properties.Get<ExifAscii>(ExifTag.CameraOwnerName);
                var exifCopyright = file.Properties.Get<ExifAscii>(ExifTag.Copyright);
                var exifDocumentName = file.Properties.Get<ExifAscii>(ExifTag.DocumentName);
                var exifImageDescription = file.Properties.Get<ExifAscii>(ExifTag.ImageDescription);
                var exifDateTime = file.Properties.Get<ExifDateTime>(ExifTag.DateTime);
                var exifDateTimeOriginal = file.Properties.Get<ExifDateTime>(ExifTag.DateTimeOriginal);

                // "1 John J Kauflin" use "John J Kauflin"
                // "5 Bands"
                // if not "Misc" use John J Kauflin
                file.Properties.Set(ExifTag.Artist, "new artist");
                file.Properties.Set(ExifTag.CameraOwnerName, "new owner");  // people
                file.Properties.Set(ExifTag.Copyright, "new copyright");
                file.Properties.Set(ExifTag.DocumentName, "new doc name");  // title
                file.Properties.Set(ExifTag.ImageDescription, "new image description");  // description

                // note the explicit cast to ushort
                //file.Properties.Set(ExifTag.ISOSpeedRatings, <ushort>200);
                file.Save(fi.FullName);

                /*
1 of 10, D:\Photos\1 John J Kauflin\2023-to-2029\2023\01 Winter\20221229_215031020_iOS.jpg

Artist              ExifAscii   string  (Authors)
CameraOwnerName     ExifAscii   string
Copyright	        ExifAscii	string	(Copyright)     ? 2021 | Gavin Phillips     year name
DocumentName	    ExifAscii	string
ImageDescription    ExifAscii	string  (Title, Subject)

BodySerialNumber	42033	0xA431	ExifAscii	string
PageName	285	0x011D	ExifAscii	string
RelatedSoundFile	40964	0xA004	ExifAscii	string

DateTimeOriginal    (Date taken)        {2022.12.29 16:50:31}
DateTime            (Date modified)     {2023.01.10 19:49:54}   Modified:  1/10/2023  7:49/54 PM


    IFD:Zeroth Tag:DateTime Name:DateTime Value:1/10/2023 7:49:54 PM                       *** when Picasa exported to a new file (when I moved to Photos) ***
    IFD:EXIF Tag:DateTimeOriginal Name:DateTimeOriginal Value:12/29/2022 4:50:31 PM        *** when the photo was taken on the camera ***
    IFD:EXIF Tag:DateTimeDigitized Name:DateTimeDigitized Value:12/29/2022 4:50:31 PM 


DateTime	306	0x0132	ExifDateTime	DateTime
DateTimeDigitized	36868	0x9004	ExifDateTime	DateTime
DateTimeOriginal	36867	0x9003	ExifDateTime	DateTime
                */

                /*
                        tStr = subIfdDirectory?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
                        if (!String.IsNullOrEmpty(tStr))
                        {
                            if (DateTime.TryParseExact(tStr, exifDateTimeFormat, null, System.Globalization.DateTimeStyles.None, out exifDateTime))
                            {
                                //Console.WriteLine($"{dateTimeStr}, tempStr = {tempStr}, dateFormat = {dateFormat}, outDateTime = {outDateTime}");
                                if (exifDateTime.CompareTo(taken) < 0)
                                {
                                    taken = exifDateTime;
                                }
                            }
                        }

                // If taken is not set, just use the file create date
                if (taken.CompareTo(fi.CreationTime) > 0)
                {
                    taken = fi.CreationTime;
                }
                */

                // Get the Picasa people face tags between specific hard-coded RDF mwg-rs tags
                //peopleStr = getTagContent(fi, @"mwg-rs:Name=""", @""" mwg-rs:Type=""Face""");
                //log($"people = {peopleStr}");

                // *** need to check for DUPLICATES and how to handle the same image under different categories
                // *** and how to build the menu structure from DB file info rather than the physical directories

                /*
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
                        fiRec.TakenDateTime = taken;
                        //fiRec.Title = "Title";
                        //fiRec.Description = "Description";
                        //fiRec.People = peopleStr;
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
                        fiRec.People = peopleStr;
                        fiRec.ToBeProcessed = 1;
                        try
                        {
                            mgr.insertFileInfoTable(fiRec);
                        }
                        catch (Exception ex)
                        {
                            log($"{index + 1} of {fileList.Count}, {fi.FullName}  *** Exception on INSERT *** ");
                        }
                    }
                    
                    conn.Close();
                }
                */

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
