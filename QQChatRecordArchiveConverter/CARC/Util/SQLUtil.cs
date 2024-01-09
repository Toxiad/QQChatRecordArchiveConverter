using QQChatRecordArchiveConverter.CARC.Module;
using SQLite;
using System.IO;
using System.Linq;

namespace QQChatRecordArchiveConverter.CARC.Util
{
    public class SQLUtil
    {
        private static SQLUtil instance;
        public static SQLUtil Instance
        {
            get
            {
                instance ??= new SQLUtil();
                return instance;
            }
        }
        private SQLiteConnection _db;
        public SQLiteConnection DB {  
            get { return  _db; }
        }
        string sqlPath = "./Data/";
        private SQLUtil()
        {
            Directory.CreateDirectory(sqlPath);
            _db = new SQLiteConnection(sqlPath + "MainDB.db");
            _db.CreateTable<DBRecord>();
            _db.CreateTable<Message>();
            if (_db.Table<DBRecord>().Count() == 0)
            {
                _db.Insert(new DBRecord());
            }
        }
        public void NewVersion()
        {
            var dbs = new SQLiteConnection(sqlPath + "MainDB.db");
            _db.InsertAll(dbs.Table<Message>().ToArray());
        }
    }
}
