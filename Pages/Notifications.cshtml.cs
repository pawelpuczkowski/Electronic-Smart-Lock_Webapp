using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;

namespace ZamekDoDrzwi.Pages
{
    // Klasa obsługująca webowe powiadomienia użytkownika
    // Dziedziczy po UserPageModel, więc wymaga aktywnej sesji użytkownika
    [IgnoreAntiforgeryToken] // Wyłączony token CSRF (bo używamy AJAX, nie klasycznego POSTa z formularza)
    public class NotificationsModel : UserPageModel
    {
        private readonly IConfiguration _config;

        public NotificationsModel(IConfiguration config)
        {
            _config = config;
        }
             
        public IActionResult OnGetLatest()
        {
            var result = new List<object>();
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0; // sprawdzenie zalogowanego użytkownika

            if (userId == 0)
                return new JsonResult(new { error = "Brak użytkownika w sesji" });

            // Połączenie z bazą MySQL
            using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
            db.Open();

            // SQL: pobranie 10 najnowszych nieprzeczytanych powiadomień webowych
            string sql = @"
                SELECT un.id, en.event_type, en.message, un.seen, en.created_at
                FROM user_notifications un
                JOIN esp_notifications en ON en.id = un.notification_id
                WHERE un.user_id = @uid
                  AND un.channel = 'web'
                  AND un.seen = 0
                ORDER BY en.created_at DESC
                LIMIT 10";

            using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@uid", userId);

            // Odczyt wyników i zbudowanie listy JSON
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add(new
                {
                    Id = Convert.ToInt32(rdr["id"]), // identyfikator powiadomienia w tabeli user_notifications
                    EventType = rdr["event_type"].ToString(), // typ zdarzenia (np. "door_open", "error", "rfid_access")
                    Message = rdr["message"].ToString(), // treść powiadomienia
                    Seen = rdr["seen"] != DBNull.Value && Convert.ToBoolean(rdr["seen"]), // flaga przeczytania
                    Time = rdr["created_at"] == DBNull.Value
                        ? ""
                        : Convert.ToDateTime(rdr["created_at"]).ToString("yyyy-MM-dd HH:mm") // format daty do wyświetlenia
                });
            }

            // Zwraca dane w formacie JSON (AJAX)
            return new JsonResult(result);
        }  

        // Oznacz pojedyncze powiadomienie jako przeczytane       
        public IActionResult OnPostMarkSeen()
        {
            // Odczyt ID powiadomienia z treści żądania (body = int)
            using var reader = new StreamReader(Request.Body);
            var body = reader.ReadToEndAsync().Result;
            if (!int.TryParse(body, out int id))
                return BadRequest(); 

            using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
            db.Open();

            // Aktualizacja statusu "seen" = 1 (przeczytane)
            string sql = "UPDATE user_notifications SET seen = 1 WHERE id = @id AND channel = 'web'";
            using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return new JsonResult(new { ok = true }); // potwierdzenie JSON
        }

     
        // Oznacz wszystkie powiadomienia webowe użytkownika jako przeczytane
      
        public IActionResult OnPostMarkAllSeen()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (userId == 0)
                return new JsonResult(new { error = "Brak użytkownika w sesji" });

            using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
            db.Open();

            // SQL - masowa aktualizacja wszystkich nieprzeczytanych powiadomień dla użytkownika
            string sql = @"
                UPDATE user_notifications 
                SET seen = 1
                WHERE user_id = @uid 
                  AND channel = 'web'
                  AND seen = 0";

            using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();

            // Zwraca prostą odpowiedź JSON do skryptu AJAX
            return new JsonResult(new { ok = true });
        }
    }
}
