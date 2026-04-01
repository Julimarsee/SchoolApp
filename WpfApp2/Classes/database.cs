using Npgsql;
using System;
using System.Data;
using System.Windows;

namespace WpfApp2
{
    internal class database
    {
        string connectionString = "host=localhost;port=5432;database=school_db;username=postgres;password=12345;sslmode=prefer;timeout=10";

        public DataView ExecuteQuery(string query, params NpgsqlParameter[] parameters)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand(query, connection))
            {
                try
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.Add(param);
                        }
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

        public DataTable DataQuery(string query, params NpgsqlParameter[] parameters)
        {
            DataTable dt = new DataTable();
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            command.Parameters.AddRange(parameters);
                        }

                        using (var adapter = new NpgsqlDataAdapter(command))
                        {
                            adapter.Fill(dt);
                        }
                    }
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
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }
    }
}