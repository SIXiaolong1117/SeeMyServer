using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeeMyServer.Datas
{
    public class SQLiteHelper
    {
        // 连接到数据库文件
        private string connectionString = "Data Source=cms.db";

        public SQLiteHelper()
        {
            // 初始化数据库连接
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
            }
        }
    }
}
