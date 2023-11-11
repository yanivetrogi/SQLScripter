using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.IO;

   
namespace SQLScripter
{
    [Serializable]
    public class ServerSettings
    {
        private static List<ServerSettings> _instance;
        private ServerSettings()
        {
        }

        public static void Save()
        {
            XmlSerializer s = new XmlSerializer(typeof(List<ServerSettings>));
            using (TextWriter w = new StreamWriter(@"Servers.config"))
            {
                s.Serialize(w, _instance);
                w.Close();
            }
        }
        
        public static void Load()
        {
            XmlSerializer s = new XmlSerializer(typeof(List<ServerSettings>));
            using (TextReader r = new StreamReader("Servers.config"))
            {
                _instance = (List<ServerSettings>)s.Deserialize(r);
                r.Close();
            }
        }

        public static List<ServerSettings> Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance;
            }
        }

        public string Databases { get; set; }
        public string ObjectTypes { get; set; }

        public int SqlServerNameLength;
        private string _SqlServer;
        public string SQLServer 
        {             
            get 
            {
                if (Program.SqlServerMaxNameLength < _SqlServer.Length)
                {
                    Program.SqlServerMaxNameLength = _SqlServer.Length;
                }
                return _SqlServer; //.ToUpper(); 
            } 
            set { _SqlServer = value; } 
        }

        public string ServerDisplayName
        {
            get
            {
                string _server_name = string.Empty;
                _server_name = SQLServer.Replace("]", string.Empty);
                _server_name = SQLServer.Replace("[", string.Empty);
                return _server_name;
            }
        }
        public bool connnectionOK { get; set; }
        public bool AuthenticationMode {get; set;}
        public string SQLUser {get; set; }
        public string SQLPassword {get; set; }
     
        public bool WriteToConsole { get; set; }
                       
        private string _ConsoleForeGroundColour;
        public string ConsoleForeGroundColour 
        {             
            get
            {
                String[] colorNames = ConsoleColor.GetNames(typeof(ConsoleColor));
                bool IsColorValid = false;
                
                foreach (string colorName in colorNames)
                {
                    ConsoleColor color = (ConsoleColor)Enum.Parse(typeof(ConsoleColor), colorName, true);
                    if (color.ToString().ToLower() == _ConsoleForeGroundColour.ToLower())
                    {
                        IsColorValid = true;
                        break;                        
                    }                    
                }

                if (IsColorValid == true)
                {
                    // If the color configured is a valid Console then use this settings
                    return _ConsoleForeGroundColour;
                }
                else
                {
                    // If the color configured is not a valid Console color then default to White
                    _ConsoleForeGroundColour = "White";
                    return _ConsoleForeGroundColour;
                }
            }

            set { _ConsoleForeGroundColour = value; }
        }       
   
    }   
}
