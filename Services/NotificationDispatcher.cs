using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZamekDoDrzwi.Services
{
    // Serwis działający w tle (BackgroundService), którego zadaniem
    // jest automatyczne wysyłanie powiadomień e-mail z bazy danych.    
    // Co 10 sekund sprawdza tabelę `user_notifications`
    // Wysyła wszystkie oczekujące wiadomości e-mail    
    // Wysyłka bazuje na klasie EmailNotificationService.
    public class NotificationDispatcher : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(IConfiguration config, ILogger<NotificationDispatcher> logger)
        {
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationDispatcher uruchomiony (obsługa e-mail).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    SendPendingEmails(_config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd w NotificationDispatcher.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        // Główna metoda przetwarzania zaległych powiadomień
        private static void SendPendingEmails(IConfiguration config)
        {
            using var db = new MySqlConnection(config.GetConnectionString("MySql"));
            db.Open();

            // pobieramy nieprzetworzone powiadomienia e-mail
            string sql = @"
                SELECT un.id AS user_notification_id, un.user_id, un.channel, en.event_type, en.message, u.email
                FROM user_notifications un
                JOIN esp_notifications en ON un.notification_id = en.id
                JOIN users u ON u.id = un.user_id
                WHERE un.channel = 'email' AND un.delivered = 0 AND u.aktywny = 1";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            var pending = new List<(int unId, string email, string eventType, string msg)>();
            while (reader.Read())
            {
                pending.Add((
                    reader.GetInt32("user_notification_id"),
                    reader.GetString("email"),
                    reader.GetString("event_type"),
                    reader.GetString("message")
                ));
            }
            reader.Close();
            // Jeśli nie ma żadnych powiadomień – nic nie rób
            if (pending.Count == 0)
            {
                Console.WriteLine("Brak e-maili do wysłania.");
                return;
            }

            Console.WriteLine($"Wysyłanie {pending.Count} e-maili...");

            foreach (var p in pending)
            {
                try
                {
                    // Generowanie tematu i treści wiadomości w zależności od typu zdarzenia
                    var mail = GetEmailTemplate(p.eventType, p.msg);
                    EmailNotificationService.Send(p.email, mail.Subject, mail.Body);

                    // Aktualizacja rekordu – oznaczenie powiadomienia jako dostarczone
                    string updateSql = "UPDATE user_notifications SET delivered = 1 WHERE id = @id";
                    using var upd = new MySqlCommand(updateSql, db);
                    upd.Parameters.AddWithValue("@id", p.unId);
                    upd.ExecuteNonQuery();

                    Console.WriteLine($"E-mail wysłany do {p.email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Błąd wysyłki do {p.email}: {ex.Message}");
                }
            }
        }

        // Pełne mapowanie event_type -> czytelne tytuły i treści
        private static (string Subject, string Body) GetEmailTemplate(string eventType, string defaultMsg)
        {
            string subject;
            string body;

            switch (eventType)
            {
                case "door_opened":
                    subject = "Zamek do drzwi – Drzwi otwarte";
                    body = "Drzwi zostały otwarte lokalnie (np. kartą RFID lub kodem PIN).";
                    break;

                case "door_remote_opened":
                    subject = "Zamek do drzwi – Drzwi otwarte zdalnie";
                    body = "Zamek został otwarty zdalnie przez administratora lub użytkownika z aplikacji webowej.";
                    break;

                case "access_denied":
                    subject = "Zamek do drzwi – Próba nieautoryzowanego dostępu";
                    body = "Wykryto próbę otwarcia drzwi bez uprawnień. Zdarzenie zostało zapisane w logach systemu.";
                    break;

                case "code_generated":
                    subject = "Zamek do drzwi – Wygenerowano kod dostępu";
                    body = "Nowy kod tymczasowy został wygenerowany. Upewnij się, że posiada go odpowiednia osoba.";
                    break;

                case "camera_photo":
                    subject = "Zamek do drzwi – Wykonano zdjęcie z kamery";
                    body = "Kamera wykryła ruch i wykonała zdjęcie. Możesz je zobaczyć w panelu administratora.";
                    break;

                case "doorbell_pressed":
                    subject = "Zamek do drzwi – Dzwonek do drzwi";
                    body = "Ktoś nacisnął przycisk dzwonka przy drzwiach.";
                    break;

                default:
                    subject = $"Zamek do drzwi – {eventType}";
                    body = defaultMsg ?? "Wystąpiło nowe zdarzenie w systemie zamka.";
                    break;
            }

            body += $"\n\nCzas zdarzenia: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n— System Zamek do drzwi IoT";
            return (subject, body);
        }
    }
}
