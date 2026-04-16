using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace ZamekDoDrzwi.Pages.Uzytkownicy
{
    public class KartyModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        // Konstruktor – przekazanie konfiguracji
        public KartyModel(IConfiguration config) => _config = config;

        // Lista wszystkich kart RFID wyświetlanych w tabeli
        public List<KartaRFID> DostepneKarty { get; set; } = new();

        // Struktura pojedynczej karty RFID
        public class KartaRFID
        {
            public int Id { get; set; }
            public string UID { get; set; }
            public string? Opis { get; set; }
            public DateTime? DataDodania { get; set; }
            public DateTime? DataWaznosci { get; set; }
            public bool Aktywna { get; set; }
            public string? Login { get; set; } // login właściciela karty (jeśli przypisana)
        }

        // Załadowanie listy wszystkich kart RFID
        public void OnGet()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Pobranie wszystkich kart RFID z przypisanymi użytkownikami
            string sql = @"
                SELECT rc.id, rc.uid, rc.opis, rc.data_dodania, rc.data_waznosci, rc.aktywna, u.login
                FROM rfid_cards rc
                LEFT JOIN users u ON rc.user_id = u.id
                ORDER BY rc.data_dodania DESC";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            // Iteracja po wynikach i zapis do listy
            while (reader.Read())
            {
                DostepneKarty.Add(new KartaRFID
                {
                    Id = reader.GetInt32("id"),
                    UID = reader.GetString("uid"),
                    Opis = reader.IsDBNull("opis") ? null : reader.GetString("opis"),
                    DataDodania = reader.IsDBNull("data_dodania") ? null : reader.GetDateTime("data_dodania"),
                    DataWaznosci = reader.IsDBNull("data_waznosci") ? null : reader.GetDateTime("data_waznosci"),
                    Aktywna = reader.GetBoolean("aktywna"),
                    Login = reader.IsDBNull("login") ? null : reader.GetString("login")
                });
            }
        }

        // Dezaktywacja karty RFID
        public IActionResult OnPostDezaktywuj(int id)
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            var cmd = new MySqlCommand("UPDATE rfid_cards SET aktywna = 0 WHERE id = @id", db);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return RedirectToPage(); // odśwież widok
        }

        // Przejście do przypisania karty użytkownikowi
        public IActionResult OnPostPrzypisz(int id)
        {
            // przekierowanie do strony przypisania karty RFID z parametrem kartaId
            return RedirectToPage("/Uzytkownicy/RFID", new { kartaId = id });
        }

        // Odpięcie karty od użytkownika
        public IActionResult OnPostOdepnij(int id)
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            var cmd = new MySqlCommand("UPDATE rfid_cards SET user_id = NULL WHERE id = @id", db);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return RedirectToPage(); // odśwież listę
        }

        // Aktywacja karty RFID
        public IActionResult OnPostAktywuj(int id)
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            var cmd = new MySqlCommand("UPDATE rfid_cards SET aktywna = 1 WHERE id = @id", db);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return RedirectToPage();
        }

        // Trwałe usunięcie karty z bazy
        public IActionResult OnPostUsun(int id)
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            var cmd = new MySqlCommand("DELETE FROM rfid_cards WHERE id = @id", db);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return RedirectToPage();
        }
    }
}
