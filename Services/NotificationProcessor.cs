using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace ZamekDoDrzwi.Services
{
    public static class NotificationProcessor
    {
        // Główne zadania:
        // wykrywa nowe wpisy z ESP (processed = 0),
        // sprawdza, którzy użytkownicy subskrybują dany typ zdarzenia,
        // tworzy wpisy w tabeli user_notifications dla odpowiednich kanałów (e-mail, web),
        // oznacza zdarzenie jako przetworzone.
        public static void ProcessNewEvents(IConfiguration config)
        {
            using var db = new MySqlConnection(config.GetConnectionString("MySql"));
            db.Open();

            // Pobranie wszystkich nowych zdarzeń z ESP
            string sql = "SELECT id, event_type, message, created_at FROM esp_notifications WHERE processed = 0 ORDER BY created_at ASC";
            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            var pending = new List<(int Id, string EventType, string Msg)>();
            while (reader.Read())
            {
                pending.Add((
                    reader.GetInt32("id"),
                    reader.GetString("event_type"),
                    reader.GetString("message")
                ));
            }
            reader.Close();

            if (pending.Count == 0)
            {
                Console.WriteLine("Brak nowych powiadomień z ESP.");
                return;
            }

            Console.WriteLine($"Znaleziono {pending.Count} zdarzeń do przetworzenia...");

            foreach (var evt in pending)
            {
                // Znajdź użytkowników subskrybujących to zdarzenie
                string usersSql = @"
                    SELECT 
                        e.user_id, 
                        e.email_enabled, 
                        e.log_enabled,
                        p.email_enabled AS global_email,
                        p.log_enabled AS global_log
                    FROM notification_events e
                    JOIN notification_preferences p ON e.user_id = p.user_id
                    WHERE e.event_type = @eventType";

                using var usersCmd = new MySqlCommand(usersSql, db);
                usersCmd.Parameters.AddWithValue("@eventType", evt.EventType);

                using var ur = usersCmd.ExecuteReader();
                var userChannels = new List<(int userId, string channel)>();

                while (ur.Read())
                {
                    int userId = ur.GetInt32("user_id");
                    bool email = ur.GetBoolean("email_enabled") && ur.GetBoolean("global_email");
                    bool web = ur.GetBoolean("log_enabled") && ur.GetBoolean("global_log");

                    if (email) userChannels.Add((userId, "email"));
                    if (web) userChannels.Add((userId, "web"));
                }
                ur.Close();

                if (userChannels.Count == 0)
                {
                    Console.WriteLine($"Brak odbiorców dla eventu '{evt.EventType}'.");
                    MarkProcessed(db, evt.Id);
                    continue;
                }

                // Utwórz wpisy user_notifications
                foreach (var (userId, channel) in userChannels)
                {
                    string insertSql = @"
                        INSERT INTO user_notifications (user_id, notification_id, channel)
                        VALUES (@uid, @nid, @ch)";
                    using var insert = new MySqlCommand(insertSql, db);
                    insert.Parameters.AddWithValue("@uid", userId);
                    insert.Parameters.AddWithValue("@nid", evt.Id);
                    insert.Parameters.AddWithValue("@ch", channel);
                    insert.ExecuteNonQuery();
                }

                MarkProcessed(db, evt.Id);
                Console.WriteLine($"Zdarzenie '{evt.EventType}' przetworzone ({userChannels.Count} wpisów).");
            }
        }
        // Oznacz rekord w tabeli `esp_notifications` jako przetworzony
        private static void MarkProcessed(MySqlConnection db, int id)
        {
            string updateSql = "UPDATE esp_notifications SET processed = 1 WHERE id = @id";
            using var upd = new MySqlCommand(updateSql, db);
            upd.Parameters.AddWithValue("@id", id);
            upd.ExecuteNonQuery();
        }
    }
}
