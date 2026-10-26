using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.IO;

namespace Idle
{
    public class DataSaver : MonoBehaviour
    {
        //Output File Name
        public string fileName;

        //Method to save, accepts any types Save(int[]), Save(string[]), Save(SampleClass[])
        public void Save(object[] objects)
        {
            //Create a local instance of a binary formatter
            BinaryFormatter binaryFormatter = new BinaryFormatter();

            // 最後まで書けた場合だけ置き換え、保存途中の失敗で旧セーブを壊さない。
            string path = Path.Combine(Application.persistentDataPath, fileName + ".bin");
            string pending = path + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(pending, FileMode.Create, FileAccess.Write))
                {
                    binaryFormatter.Serialize(stream, objects);
                    stream.Flush();
                }
                if (File.Exists(path)) File.Replace(pending, path, null);
                else File.Move(pending, path);
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
            }
        }
        //Method to load
        public object[] Load()
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();

            //Check for file existence
            if (!File.Exists(Application.persistentDataPath + "/" + fileName + ".bin"))
                return null;

            using (FileStream fileStream = (File.Open(Application.persistentDataPath + "/" + fileName + ".bin", FileMode.Open)))
            {
                object[] obj = binaryFormatter.Deserialize(fileStream) as object[]; //Get the array of data from the file

                return obj; //We return them and load in DataManager
            }
        }

    }
}
