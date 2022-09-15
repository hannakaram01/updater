using System;
using System.Net;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using System.Data;
using System.Windows.Forms;
using System.Xml;
using System.IO.Compression;
using System.Linq;

namespace POS_Updater
{
    class FileProperties
    {
        string POSlocalPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft Dynamics AX\60\Retail POS\Extensions\";
        private string connectionString = ConfigurationManager.ConnectionStrings["defaultconnection"].ConnectionString;
        public string terminal = ConfigurationManager.AppSettings["TerminalId"];
        public string tempPath = Path.GetTempPath();
        public string guidXmlGeneral = Guid.NewGuid().ToString();
        public string guidFilesUpdate = string.Empty;
        public string guidFileZip = string.Empty;
        bool firstToDownload = false;
        bool isOnline = true;
        string serverUpdatePath = ConfigurationManager.AppSettings["ServerUpdatePath"];
        string filePathLocalServer = ConfigurationManager.AppSettings["LocalUpdatePath"];
        int result = 0;           //el return del ID axversions (foreign key)
        string gP = string.Empty; //Parametro guidPatch que sera incorporado a la DB localfiles - foreign key search
        string guidLocal;

        public void MainRun()
        {
            Directory.CreateDirectory(filePathLocalServer);
            
            if (Directory.GetFiles(filePathLocalServer).Length > 1 && File.Exists(filePathLocalServer + @"\" + "UPDATEINFO.xml"))
            {
                isOnline = false;
                try
                {
                    LoadXmlGeneral(filePathLocalServer);
                    File.Delete(filePathLocalServer + @"\UPDATEINFO.xml");

                }
                catch (Exception e)
                {
                    Log("Error en carga Local", e.Message);
                }
            }

            isOnline = true;
            DownloadXmlGeneral();

            string path = tempPath + guidXmlGeneral;
            LoadXmlGeneral(path);

        }
    
        public void LoadXmlGeneral(string localOrOnlinePath)
        {
            string query = "GC_Sp_AxVersions";
            // DateTime patchDate;
            string patchName;

            string readPath = localOrOnlinePath + @"\" + "UPDATEINFO.xml";
            XmlDocument doc = new XmlDocument();

            try { doc.Load(readPath); }
            catch (Exception e)
            { Log("Error de lectura del UPDATEINFO.xml. Verificar Conexión",e.Message); }

            XmlNodeList itemNodes = doc.SelectNodes("//Updates/Patch");
            foreach (XmlNode nodo in itemNodes)
            {
                if (itemNodes != null)
                {
                    patchName = nodo.Attributes["name"].Value;
                    // patchDate = DateTime.Parse(nodo.Attributes["date"].Value);

                    using (var connection = new SqlConnection(connectionString))
                    {
                        SqlCommand command = new SqlCommand(query, connection);
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@TERMINALID", SqlDbType.NVarChar).Value = terminal;
                        command.Parameters.Add("@GUID", SqlDbType.NVarChar).Value = patchName;

                        connection.Open();
                        SqlDataReader reader = command.ExecuteReader();

                        while (reader.Read())
                        {
                            int reg = reader.GetInt32(0);
                            if (reg == 0)  //Si no esta el zip actualizado para ese terminal, procede.
                            {
                                string querySavePathDb = "GC_Sp_InsertAxVersions";
                                SqlCommand command2 = new SqlCommand(querySavePathDb, connection);
                                command2.CommandType = CommandType.StoredProcedure;
                                command2.Parameters.Add("@TERMINALID", SqlDbType.NVarChar).Value = terminal;
                                command2.Parameters.Add("@GUID", SqlDbType.NVarChar).Value = patchName;
                                command2.Parameters.Add("@STATUS", SqlDbType.Int).Value = 0;
                                command2.Parameters.Add("@DATAAREAID", SqlDbType.NVarChar).Value = ConfigurationManager.AppSettings["DATAAREAID"];

                                var returnParameter = command2.Parameters.Add("@return_value", SqlDbType.Int);
                                returnParameter.Direction = ParameterDirection.ReturnValue;

                                command2.ExecuteNonQuery();

                                result = (int)returnParameter.Value;
                                string pathx = string.Empty;
                                if (isOnline == true)
                                {
                                    guidFilesUpdate = Guid.NewGuid().ToString();
                                    DownloadXmlSpecific(patchName + ".zip");
                                    string sourcePath = tempPath + guidFilesUpdate + @"/" + guidFileZip;
                                    string destinyPath = tempPath + guidFilesUpdate;
                                    ZipFile.ExtractToDirectory(sourcePath, destinyPath);
                                    pathx = tempPath + guidFilesUpdate + @"\updateinfo.xml";
                                }

                                else
                                {
                                    guidLocal = Guid.NewGuid().ToString();
                                    Directory.CreateDirectory(filePathLocalServer+@"\"+guidLocal);
                                    pathx = filePathLocalServer + @"\" + guidLocal + @"\updateinfo.xml";
                                    ZipFile.ExtractToDirectory(filePathLocalServer + @"\" + patchName + ".zip", filePathLocalServer + @"\" + guidLocal);
                                    
                                }

                                LoadXmlSpecific(pathx, patchName);

                                if (isOnline==true)
                                {
                                    Directory.Delete(tempPath + guidFilesUpdate,true);
                                }
                                else
                                {                               
                                    Directory.Delete(filePathLocalServer + @"\" + guidLocal,true);
                                    File.Delete(filePathLocalServer + @"\" + patchName + ".zip");

                                }
                               
                            }

                        }
                        reader.Close();
                        connection.Close();
                    }
                }
            }
        }

        public void LoadXmlSpecific(string pathx, string patchName)
        {

            XmlDocument docx = new XmlDocument();
          //  try
          //  {
                docx.Load(pathx);

          //  }
          //  catch (Exception e)
          //  {
          //      Log("Error de lectura", e.Message);
          //  }

            XmlNodeList updateNode = docx.SelectNodes("//Updates/Update");
            int dllcount = 0;
            if (updateNode.Count != 0)
            {
                foreach (XmlNode nodox in updateNode)
                {
                    gP = nodox["guidpatch"].InnerText;
                    string fileName = nodox["filename"].InnerText;
                    string sourceFile = string.Empty;

                    sourceFile = isOnline == true ? tempPath + guidFilesUpdate + @"\" + fileName : filePathLocalServer + @"\" + guidLocal + @"\" + fileName;       

                    if (nodox["codepath"].InnerText == "dll")
                    {

                        string destFile = POSlocalPath + fileName;

                        File.Copy(sourceFile, destFile, true);

                        SaveFileMovInBd(nodox);
                        dllcount++;

                    }

                    if (nodox["codepath"].InnerText == "sql")
                    {

                        string server = ConfigurationManager.AppSettings["Data Source"];
                        string dbName = ConfigurationManager.AppSettings["Initial Catalog"];
                        string cmdCommand = "sqlcmd -S " + server + " -d " + dbName + " -i " + sourceFile;

                        ExecSqlFile(cmdCommand);

                        SaveFileMovInBd(nodox);

                    }
                    if (nodox["codepath"].InnerText == "config") //Copia cualquier archivo en la carpeta de la aplicación y es ideal para App.config
                    {

                        string thisProgramPath = Application.StartupPath + @"\" + fileName;
                        File.Copy(sourceFile, thisProgramPath, true);
                        SaveFileMovInBd(nodox);
                    }

                    if (nodox["codepath"].InnerText == "doc") 
                    {

                        string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\" + fileName;
                        File.Copy(sourceFile, myDocumentsPath, true);
                        SaveFileMovInBd(nodox);

                    }

                    if (nodox["codepath"].InnerText == "desk")

                    {
                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\" + fileName;
                        File.Copy(sourceFile, desktop, true);
                        SaveFileMovInBd(nodox);

                    }

                    if (nodox["codepath"].InnerText == "esp")

                    {
                        try
                        {
                            string pathconfig = ConfigurationManager.AppSettings["pathdestiny"];
                            string destinyPath = pathconfig != null ? pathconfig : nodox["path"].InnerText; //Busca el pathdestiny en el app.config, sino en el xml especifico

                            File.Copy(sourceFile, destinyPath, true);
                            SaveFileMovInBd(nodox);

                        }catch (Exception e){ Log("Error al eliminar un archivo", e.Message); }
                    }
                }

            }
            XmlNodeList removeNode = docx.SelectNodes("//Updates/Remove");

            if (removeNode.Count != 0)
            {
                foreach (XmlNode nodox in removeNode)
                {
                    if (nodox["codepath"].InnerText == "dll")
                    {

                        File.Delete(POSlocalPath + nodox["filename"].InnerText);

                        SaveFileMovInBd(nodox);

                    }
                    else
                    {
                        try
                        {
                            File.Delete(nodox["pathremove"].InnerText);
                            SaveFileMovInBd(nodox);
                        }
                        catch (Exception e)
                        {

                            Log("Error con la ruta para eliminar archivos.", e.Message);
                        }
                       
                    }

                }
                dllcount++;
            }

            if (dllcount > 0)
            {
                GetFilesInfo();
            }

            int extNumFiles = Directory.GetFiles(POSlocalPath).Length;

            DirectoryInfo info = new DirectoryInfo(POSlocalPath);
            long extTotalSize = info.EnumerateFiles().Sum(file => file.Length);

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string queryUpdateAxV = "GC_Sp_UpdateAxVersions";
                SqlCommand update = new SqlCommand(queryUpdateAxV, connection);
                update.CommandType = CommandType.StoredProcedure;
                update.Parameters.Add("@TERMINALID", SqlDbType.NVarChar).Value = terminal;
                update.Parameters.Add("@GUID", SqlDbType.NVarChar).Value = patchName;
                update.Parameters.Add("@CURRENTNFILES", SqlDbType.Int).Value = extNumFiles;
                update.Parameters.Add("@CURRENTFOLDERSIZE", SqlDbType.BigInt).Value = extTotalSize;

                update.ExecuteNonQuery();
                connection.Close();
            }

        }
       
        public void ExecSqlFile(string command)
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command);
            // Indicamos que la salida del proceso se redireccione en un Stream
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            //Indica que el proceso no despliegue una pantalla negra (El proceso se ejecuta en background)
            procStartInfo.CreateNoWindow = false;
            //Inicializa el proceso
            Process proc = new Process();
            proc.StartInfo = procStartInfo;
            proc.Start();

        }

        public void SaveFileMovInBd(XmlNode nodox)
        {
            string query2 = "GC_Sp_DetailPatchUpdate";
            using (var connection = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand(query2, connection);
                connection.Open();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SIZE", SqlDbType.Decimal).Value = nodox["size"].InnerText; ;
                cmd.Parameters.Add("@VERSIONFILE", SqlDbType.NVarChar).Value = nodox["versionfile"].InnerText;
                cmd.Parameters.Add("@VERSIONPRODUCT", SqlDbType.NVarChar).Value = nodox["versionproduct"].InnerText;
                cmd.Parameters.Add("@CREATIONDATE", SqlDbType.DateTime).Value = nodox["creationdate"].InnerText;
                cmd.Parameters.Add("@TERMINALID", SqlDbType.NVarChar).Value = terminal;
                cmd.Parameters.Add("@GUIDPATCH", SqlDbType.NVarChar).Value = nodox["guidpatch"].InnerText;
                cmd.Parameters.Add("@FILENAME", SqlDbType.NVarChar).Value = nodox["filename"].InnerText;
                if (nodox.Name == "Update")
                {
                    cmd.Parameters.Add("@ACTION", SqlDbType.Int).Value = 1;
                }
                if (nodox.Name == "Remove")
                {
                    cmd.Parameters.Add("@ACTION", SqlDbType.Int).Value = 2;
                }

                cmd.Parameters.Add("@EXTENSION", SqlDbType.NVarChar).Value = nodox["ext"].InnerText;
                cmd.Parameters.Add("@DATAAREAID", SqlDbType.NVarChar).Value = ConfigurationManager.AppSettings["DATAAREAID"];
                cmd.Parameters.Add("@returned", SqlDbType.Int).Value = result;
                cmd.Parameters.Add("@DESCRIPTION", SqlDbType.NVarChar).Value = nodox["description"].InnerText;
                cmd.Parameters.Add("@CODEPATH", SqlDbType.NVarChar).Value = nodox["codepath"].InnerText;


                cmd.ExecuteNonQuery();
                connection.Close();

            }

        }

        public void GetFilesInfo()
        {

            string query = "[dbo].[GC_SP_localFilesInfo]";
            using (var connection = new SqlConnection(connectionString))
            {
                DirectoryInfo files = new DirectoryInfo(POSlocalPath);

                foreach (var ff in files.GetFiles("*", SearchOption.AllDirectories))
                {

                    var command = new SqlCommand(query, connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@FILENAME", SqlDbType.NVarChar).Value = ff.Name;
                    command.Parameters.Add("@SIZE", SqlDbType.Decimal).Value = ff.Length;
                    command.Parameters.Add("@CREATIONDATE", SqlDbType.DateTime).Value = ff.LastWriteTime;

                    command.Parameters.Add("@GUIDPATCH", SqlDbType.NVarChar).Value = gP;
                    
                    string direccion = ff.FullName;

                    if (FileVersionInfo.GetVersionInfo(direccion).FileVersion != null)
                    { command.Parameters.Add("@VERSIONFILE", SqlDbType.NVarChar).Value = FileVersionInfo.GetVersionInfo(direccion).FileVersion; }
                    else { command.Parameters.Add("@VERSIONFILE", SqlDbType.NVarChar).Value = "NA"; }

                    if (FileVersionInfo.GetVersionInfo(direccion).ProductVersion != null)
                    { command.Parameters.Add("@VERSIONPRODUCT", SqlDbType.NVarChar).Value = FileVersionInfo.GetVersionInfo(direccion).ProductVersion; }
                    else { command.Parameters.Add("@VERSIONPRODUCT", SqlDbType.NVarChar).Value = "NA"; }

                    command.Parameters.Add("@TERMINALID", SqlDbType.NVarChar).Value = terminal;
                    command.Parameters.Add("@DATAAREAID", SqlDbType.NVarChar).Value = ConfigurationManager.AppSettings["DATAAREAID"];

                    connection.Open();
                    command.ExecuteNonQuery();
                    connection.Close();

                }
            }
        }

        public void DownloadXmlGeneral()
        {
            firstToDownload = true;
            DownloadFunction(guidXmlGeneral, "UPDATEINFO.xml");
        }

        public void DownloadXmlSpecific(string guidzip)
        {
            firstToDownload = false;
            DownloadFunction(guidFilesUpdate, guidzip);
        }

        public void DownloadFunction(string guid, string guidzip)
        {

            Directory.CreateDirectory(tempPath + guid);
            string path = tempPath + guid + @"\" + guidzip;
            string url = serverUpdatePath + guidzip;
            WebClient webclient = new WebClient();
            guidFileZip = guidzip;

            try
            {
                webclient.DownloadFile(new Uri(url), path);
            }
            catch(Exception e)
            {
                Log("Problemas de Descarga", e.Message);
            }
        }

        public void Log(string title, string logMessage)
        {

            using (StreamWriter w = File.AppendText(filePathLocalServer + @"\log.txt"))
            {
                w.Write("\r\nLog Entry : ");
                w.WriteLine($"[{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}]");
                w.WriteLine("[Error Point]: " + title);
                w.WriteLine($"[Message Error]: {logMessage}");
                w.WriteLine("-------------------------------");

            }

        }


    }
}
