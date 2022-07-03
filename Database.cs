using MySqlConnector;
using static DifficultyCalculator.Globals;

namespace DifficultyCalculator
{
    public class Database
    {
        public static async Task<MySqlConnection> GetDatabaseConnection()
        {
            string host = Settings.SQLHost;
            string user = Settings.SQLUser;
            string db = Settings.SQLDatabase;
            string pass = Settings.SQLPassword;

            var connection = new MySqlConnection($"Server={host};Database={db};User ID={user};Password={pass};Pooling=true;");
            await connection.OpenAsync();

            return connection;
        }
    }
}