using System;
using System.Net;
using System.Net.Mail;

namespace ZamekDoDrzwi.Services
{   
    // Klasa odpowiedzialna za wysyłanie wiadomości e-mail  
    // Powiadomienia o zdarzeniach
    // Kody weryfikacji 2FA
    // Resetowanie haseł 

    public class EmailNotificationService
    {
        public static void Send(string to, string subject, string body)
        {          
            // Konfiguracja serwera SMTP (Gmail)         
            string smtpHost = ""; // adres serwera SMTP
            int smtpPort = ;                 // port TLS
            string smtpUser = ""; // konto nadawcy
            string smtpPass = "";          
            // Konfiguracja klienta SMTP            
            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass), // dane logowania
                EnableSsl = true                                         // szyfrowanie TLS
            };
                        
            // Przygotowanie wiadomości
            using var mail = new MailMessage
            {
                From = new MailAddress(
                    smtpUser,
                    "Zamek do drzwi – powiadomienia" // nazwa nadawcy wyświetlana u odbiorcy
                ),
                Subject = subject,
                Body = body,
                IsBodyHtml = false // treść jako tekst (bez HTML)
            };

            // Dodaj odbiorcę
            mail.To.Add(to);
          
            // Wysyłka e-maila           
            try
            {
                client.Send(mail);
                Console.WriteLine($"E-mail wysłany do: {to}");
            }
            catch (Exception ex)
            {
                // Obsługa błędów (np. brak połączenia, błędne dane SMTP)
                Console.WriteLine($"Błąd wysyłki e-maila: {ex.Message}");
                throw; // Przekazanie błędu wyżej – pozwala na logowanie w systemie
            }
        }
    }
}
