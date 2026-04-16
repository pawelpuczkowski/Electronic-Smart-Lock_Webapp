using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System;

namespace ZamekDoDrzwi.Services
{
    public static class AccessLogService
    {
        //Zapisuje zdarzenie do tabeli access_logs       
        public static void Log(
            IConfiguration config,
            string typZdarzenia,
            string komponent,
            string szczegoly,
            string metoda,
            string powod,
            bool sukces,
            string akcja,
            int? uzytkownikId = null,
            int? guestId = null)
        {
            try
            {
                using var conn = new MySqlConnection(config.GetConnectionString("MySql"));
                conn.Open();

                string sql = @"
                    INSERT INTO access_logs 
                    (czas, typ_zdarzenia, komponent, szczegoly, metoda, powod, sukces, akcja, uzytkownik_id, guest_id)
                    VALUES (NOW(), @typ, @komp, @szcz, @metoda, @powod, @sukces, @akcja, @uid, @gid)";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@typ", typZdarzenia);
                cmd.Parameters.AddWithValue("@komp", komponent);
                cmd.Parameters.AddWithValue("@szcz", szczegoly ?? "");
                cmd.Parameters.AddWithValue("@metoda", metoda ?? "");
                cmd.Parameters.AddWithValue("@powod", powod ?? "");
                cmd.Parameters.AddWithValue("@sukces", sukces);
                cmd.Parameters.AddWithValue("@akcja", akcja ?? "");
                cmd.Parameters.AddWithValue("@uid", uzytkownikId);
                cmd.Parameters.AddWithValue("@gid", guestId);

                cmd.ExecuteNonQuery();

                Console.WriteLine($"Zapisano log: {typZdarzenia} ({(sukces ? "OK" : "BŁĄD")})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd zapisu logu: {ex.Message}");
            }
        }
    }
}
