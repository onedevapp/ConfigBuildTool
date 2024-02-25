using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/**
*  C# to read a properties file that has each property on a separate line followed by an equals sign and the value
*  https://stackoverflow.com/a/7696370
*/
namespace OneDevApp.GameConfig
{
    public class PropertiesLoader
    {

        private Dictionary<string, string> list;
        private string filename;

        public PropertiesLoader(string file)
        {
            reload(file);
        }

        public string get(string field, string defValue)
        {
            return (get(field) == null) ? (defValue) : (get(field));
        }
        public string get(string field)
        {
            return (list.ContainsKey(field)) ? (list[field]) : (null);
        }

        public bool IsPropertiesLoaded()
        {
            return list != null && list.Count > 0;
        }

        public void set(string field, Object value)
        {
            if (!list.ContainsKey(field))
                list.Add(field, value.ToString());
            else
                list[field] = value.ToString();
        }

        public void Save()
        {
            Save(this.filename);
        }

        public void Save(string filename)
        {
            this.filename = filename;

            if (!System.IO.File.Exists(filename))
                System.IO.File.Create(filename).Dispose();

            using (TextWriter writer = new StreamWriter(filename, false))
            {
                foreach (string prop in list.Keys.ToArray())
                    if (!string.IsNullOrWhiteSpace(list[prop]))
                        writer.WriteLine(prop + "=" + list[prop]);
            }
        }

        public void reload()
        {
            reload(this.filename);
        }

        public void reload(string filename)
        {
            this.filename = filename;
            list = new Dictionary<string, string>();

            if (System.IO.File.Exists(filename))
                loadFromFile(filename);
            //else
            //    System.IO.File.Create(filename);
        }

        private void loadFromFile(string file)
        {
            using (var reader = new StreamReader(file))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if ((!string.IsNullOrEmpty(line)) &&
                    (!line.StartsWith(";")) &&
                    (!line.StartsWith("#")) &&
                    (!line.StartsWith("'")) &&
                    (line.Contains('=')))
                    {
                        int index = line.IndexOf('=');
                        string key = line.Substring(0, index).Trim();
                        string value = line.Substring(index + 1).Trim();

                        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                            (value.StartsWith("'") && value.EndsWith("'")))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }

                        try
                        {
                            //ignore dublicates
                            list.Add(key, value);
                        }
                        catch { }
                    }
                }
            }
        }

    }
}
