using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace SQLScripter
{
    public class Servers
    {
        List<ServerSettings> _serverSettings = new List<ServerSettings>(32);

        public List<ServerSettings> ServerSettingsList
        {
            get { return _serverSettings; }
            set { _serverSettings = value; }
        }

        public Servers()
        {
            //ServerSettings obj = new ServerSettings();
            //obj.AuthenticationMode = true;
            //obj.SendMail = false;
            //obj.SmtpToList = "Y.Etrogi@sd.com";
            //obj.SQLServer = "ws-yanive";
            //obj.SQLUser = string.Empty;
            //obj.SQLPassword = string.Empty;           
            //_serverSettings.Add(obj);
        }


        public void Save()
        {
            XmlSerializer s = new XmlSerializer(typeof(List<ServerSettings>));
            using (TextWriter w = new StreamWriter(@"Servers.config"))
            {
                s.Serialize(w, _serverSettings);
                w.Close();
            }
        }

        public void Load()
        {
            string _full_file_name = Path.Combine(new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName + @"\Configuration", "Servers.config");

            XmlSerializer s = new XmlSerializer(typeof(List<ServerSettings>));
            using (TextReader r = new StreamReader(_full_file_name))
            {
                _serverSettings = (List<ServerSettings>)s.Deserialize(r);
                r.Close();
            }
        }

    }
}
