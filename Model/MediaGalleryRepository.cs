/*==============================================================================
  (C) Copyright 2022 John J Kauflin, All rights reserved.
--------------------------------------------------------------------------------
  DESCRIPTION:  
--------------------------------------------------------------------------------
DESCRIPTION:  Class to handle interactions with the database repository
    using Dapper and Dapper Contrib NuGet libraries for the database queries
    and model classes for the tables.  This class counts on connection
    being handled in the calling class, but it will abstract the query
    logic and the "Dapper" work so calling apps don't have to include that

-------------------------------------------------------------------------------
 2022-12-27 JJK   Initial version
===============================================================================*/

using MySqlConnector;

using Dapper;
using Dapper.Contrib;
using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    public class MediaGalleryRepository
    {
        // Connection created and opened by calling app and passed to this library class when instantiated
        private MySqlConnection conn;

        public MediaGalleryRepository(MySqlConnection inConnection)
        {
            conn = inConnection;
        }

        public void setConnection(MySqlConnection inConnection)
        {
            conn = inConnection;
        }

        public List<ConfigParamTable> getConfigParam()
        {
            return conn.Query<ConfigParamTable>("SELECT ConfigName,ConfigValue FROM ConfigParam").AsList();
        }

        public void UpdateConfigParamValue(string configName, string configValue)
        {
            string sql = String.Format("SELECT * FROM ConfigParam"
                            + " WHERE ConfigName = '{0}' ", configName);
            //Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss.fff}, sql = {1}", DateTime.Now, sql);

            var configParam = conn.QuerySingleOrDefault<ConfigParamTable>(sql);
            if (configParam != null)
            {
                configParam.ConfigValue = configValue;
                conn.Update(configParam);
            }
        }

        public bool updFileInfoToBeProcessed(string Name, bool ToBeProcessed)
        {
            bool updSuccess = true;
            var fiRec = conn.Get<FileInfoTable>(Name);
            if (fiRec != null)
            {
                fiRec.ToBeProcessed = 0;
                if (ToBeProcessed)
                {
                    fiRec.ToBeProcessed = 1;
                }
                updSuccess = conn.Update(fiRec);
            }

            return updSuccess;
        }

        public FileInfoTable getFileInfoTable(string Name)
        {
            /*
            string sql = String.Format("SELECT * FROM FileInfo"
                            + " WHERE Name = '{0}' ", Name);
            //Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss.fff}, sql = {1}", DateTime.Now, sql);
            return conn.QuerySingleOrDefault<FileInfoTable>(sql);
            */
            return conn.Get<FileInfoTable>(Name);
        }

        public bool updateFileInfoTable(FileInfoTable fiRec)
        {
            return conn.Update(fiRec);
        }
        public long insertFileInfoTable(FileInfoTable fiRec)
        {
            return conn.Insert(fiRec);
        }


        public List<FileInfoTable> getFileInfoTableList(int maxRows = 100)
        {
            string sql = String.Format($"SELECT * FROM FileInfo WHERE ToBeProcessed > 0 LIMIT {maxRows}; ");
            //Console.WriteLine("${DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}, sql = {sql}");

            return conn.Query<FileInfoTable>(sql).AsList();
        }

    } // public class MediaGalleryRepository
} // namespace MediaGalleryAdmin.Model
