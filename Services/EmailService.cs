using System.Net;
using System.Net.Mail;
public static class EmailService
{   
    public static void Send(string to, string subject, string body)
    {
        //Konfiguracja serwera SMTP (Gmail)        
        string smtpHost = "smtp.gmail.com";              // Adres serwera SMTP
        int smtpPort = 587;                              // Port TLS (STARTTLS)
        string smtpUser = "zamekdodrzwiiot@gmail.com";   // Konto nadawcy
        string smtpPass = "jrvsvuyfbexkwqof";            // Hasło aplikacji Gmail
        // string smtpPass = "ZamekdodrzwiIoT123";               
        // Inicjalizacja klienta SMTP       
        var client = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUser, smtpPass), // dane logowania
            EnableSsl = true                                         // włączenie szyfrowania TLS
        };

      
        // Przygotowanie wiadomości
        var mailMessage = new MailMessage
        {
            From = new MailAddress(
                smtpUser,
                "Zamek do drzwi - logowanie" // nazwa nadawcy widoczna u odbiorcy
            ),
            Subject = subject,               
            Body = body,                     
            IsBodyHtml = false               // wyłącz HTML
        };
            
        mailMessage.To.Add(to);
        // Próba wysyłki wiadomości     
        try
        {
            client.Send(mailMessage);
            System.Diagnostics.Debug.WriteLine($"E-mail wysłany do: {to}");
        }
        catch (Exception ex)
        {
            // Obsługa błędów (np. brak Internetu, błąd SMTP)
            System.Diagnostics.Debug.WriteLine($"Błąd wysyłki e-maila: {ex.Message}");

            // Rzucenie błędu dalej umożliwia logowanie w warstwie wyżej
            throw;
        }
    }
}
