using System;
using System.Threading;
using Npgsql;
using StackExchange.Redis;
using Newtonsoft.Json;

class Program
{
    static void Main()
    {
        var pgsql = OpenDbConnection();
        var redis = OpenRedisConnection();
        var db = redis.GetDatabase();

        Console.WriteLine("Worker started. Watching Redis for votes...");

        while (true)
        {
            string json = db.ListLeftPop("votes");

            if (json != null)
            {
                var vote = JsonConvert.DeserializeAnonymousType(json, new { voter_id = "", vote = "" });
                Console.WriteLine($"Processing vote '{vote.vote}' from voter '{vote.voter_id}'");
                SaveVote(pgsql, vote.voter_id, vote.vote);
            }
            else
            {
                Thread.Sleep(500); // nothing to do — don't hammer Redis, just check again shortly
            }
        }
    }

    static NpgsqlConnection OpenDbConnection()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var connectionString = $"Host={host};Username=postgres;Password=postgres;Database=postgres";
        NpgsqlConnection connection;

        while (true)
        {
            try
            {
                connection = new NpgsqlConnection(connectionString);
                connection.Open();
                break;
            }
            catch (Exception)
            {
                Console.WriteLine("Postgres not ready yet, retrying in 1s...");
                Thread.Sleep(1000);
            }
        }

        Console.WriteLine("Connected to Postgres.");

        var createTable = connection.CreateCommand();
        createTable.CommandText = @"
            CREATE TABLE IF NOT EXISTS votes (
                id VARCHAR(255) NOT NULL UNIQUE,
                vote VARCHAR(255) NOT NULL
            )";
        createTable.ExecuteNonQuery();

        return connection;
    }

    static ConnectionMultiplexer OpenRedisConnection()
    {
        var host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";

        while (true)
        {
            try
            {
                Console.WriteLine("Connecting to Redis...");
                return ConnectionMultiplexer.Connect(host);
            }
            catch (Exception)
            {
                Console.WriteLine("Redis not ready yet, retrying in 1s...");
                Thread.Sleep(1000);
            }
        }
    }

    static void SaveVote(NpgsqlConnection connection, string voterId, string vote)
    {
        var command = connection.CreateCommand();
        try
        {
            command.CommandText = "INSERT INTO votes (id, vote) VALUES (@id, @vote)";
            command.Parameters.AddWithValue("@id", voterId);
            command.Parameters.AddWithValue("@vote", vote);
            command.ExecuteNonQuery();
        }
        catch (PostgresException)
        {
            // insert failed because this voter_id already exists — update instead
            var update = connection.CreateCommand();
            update.CommandText = "UPDATE votes SET vote = @vote WHERE id = @id";
            update.Parameters.AddWithValue("@vote", vote);
            update.Parameters.AddWithValue("@id", voterId);
            update.ExecuteNonQuery();
        }
    }
}