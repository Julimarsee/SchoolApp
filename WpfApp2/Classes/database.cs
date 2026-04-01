using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace WpfApp2
{
    internal class database
    {
        string connectionString = "host=localhost;port = 5432;database=school_db;username = postgres;password =12345;sslmode=prefer;timeout = 10";
        public DataView ExecuteQuery(string query, params NpgsqlParameter[] parameters)
        {
            using (var connection = new NpgsqlConnection(connectionString))

            using (var command = new NpgsqlCommand(query, connection))
            {
                try
                {
                    foreach(var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }

                    connection.Open();
                    DataTable dataTable = new DataTable();
                    dataTable.Load(command.ExecuteReader());
                    return dataTable.DefaultView;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}");
                    return null;
                }
            }
        }

        public DataTable DataQuery(string query)
        {

            DataTable dt = new DataTable();
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                        using (var adapter = new NpgsqlDataAdapter(command))
                            adapter.Fill(dt);
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}");
            }

            return dt;
        }

        public void ExecuteNonQuery(string query, params NpgsqlParameter[] parameters)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                using (var command = new NpgsqlCommand(query, connection))
                {
                    connection.Open();

                    if (parameters != null && parameters.Length > 0)
                        command.Parameters.AddRange(parameters);

                    int changed = command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

    }
}
