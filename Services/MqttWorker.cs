using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using MySql.Data.MySqlClient;

public class MqttWorker : BackgroundService
{
    private readonly ILogger<MqttWorker> _logger;
    private readonly IConfiguration _settings;
    private IMqttClient? _mqttClient;
    private readonly string broker = "zamekdodrzwi.pl";
    private readonly int port = 1883;
    private readonly string username = "zamek_user";
    private readonly string password = "Zamekdodrzwi123!";

    public MqttWorker(ILogger<MqttWorker> logger, IConfiguration settings)
    {
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .WithCredentials(username, password)
            .WithClientId("ZAMEK_BACKEND")
            .Build();

        _mqttClient.ConnectedAsync += async e =>
        {
    _logger.LogInformation("Połączono z MQTT brokerem.");
    await _mqttClient.SubscribeAsync("zamek/logs");
    await _mqttClient.SubscribeAsync("zamek/status/#");
    await _mqttClient.SubscribeAsync("zamek/notify");
    await _mqttClient.SubscribeAsync("zamek/rfid/access");
    await _mqttClient.SubscribeAsync("zamek/rfid/uid");
    await _mqttClient.SubscribeAsync("zamek/rfid/verify");
    await _mqttClient.SubscribeAsync("zamek/sms/sent");
    await _mqttClient.SubscribeAsync("zamek/access/#");
    _logger.LogInformation("Subskrybowano tematy MQTT.");
};
        _mqttClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("Rozłączono z brokerem MQTT. Próba ponownego połączenia za 5s...");
            await Task.Delay(5000, stoppingToken);
            try { await _mqttClient.ConnectAsync(options, stoppingToken); } catch { }
        };

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _logger.LogInformation($"MQTT: {topic} => {payload}");

            try
            {
                if (topic.StartsWith("zamek/logs"))
                    await SaveLogToDatabase(payload);
                else if (topic.StartsWith("zamek/status"))
                    await UpdateDeviceStatus(payload);
                else if (topic.StartsWith("zamek/notify"))
                    await SaveNotification(payload);
                else if (topic == "zamek/rfid/access")
                    await HandleRfidAccess(payload);              
                else if (topic == "zamek/rfid/verify")
                    await HandleRfidVerify(payload);
                else if (topic == "zamek/rfid/uid")
                    await HandleRfidAccess(payload);
                else if (topic == "zamek/sms/sent")
                    await HandleSmsSent(payload);
                    else if (topic == "zamek/access/code")
{
    await HandleAccessCode(payload);
}

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przetwarzania wiadomości MQTT");
            }
        };

        try
        {
            await _mqttClient.ConnectAsync(options, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się połączyć z brokerem MQTT.");
        }
    }

    //Publikuj
    public async Task PublishAsync(string topic, string payload)
{
    if (_mqttClient == null || !_mqttClient.IsConnected)
        return;

    var mqttMessage = new MqttApplicationMessageBuilder()
        .WithTopic(topic)
        .WithPayload(payload)
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
        .Build();

    await _mqttClient.PublishAsync(mqttMessage);

    _logger.LogInformation($"MQTT publish: {topic} => {payload}");
}


     private async Task SaveLogToDatabase(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string typ = root.GetProperty("typ_zdarzenia").GetString() ?? "nieznany";
            string komponent = root.GetProperty("komponent").GetString() ?? "brak";
            string szczegoly = root.GetProperty("szczegoly").GetString() ?? "";
            string metoda = root.TryGetProperty("metoda", out var m) ? m.GetString() ?? "" : "";
            string powod = root.TryGetProperty("powod", out var p) ? p.GetString() ?? "" : "";
            int sukces = root.TryGetProperty("sukces", out var s) ? s.GetInt32() : 1;
            string akcja = root.TryGetProperty("akcja", out var a) ? a.GetString() ?? "" : "";

            using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
            await db.OpenAsync();

            const string query = @"
INSERT INTO access_logs (typ_zdarzenia, komponent, szczegoly, metoda, powod, sukces, akcja)
VALUES (@typ, @komp, @szcz, @metoda, @powod, @sukces, @akcja)";


            using var cmd = new MySqlCommand(query, db);
            cmd.Parameters.AddWithValue("@typ", typ);
            cmd.Parameters.AddWithValue("@komp", komponent);
            cmd.Parameters.AddWithValue("@szcz", szczegoly);
            cmd.Parameters.AddWithValue("@metoda", metoda);
            cmd.Parameters.AddWithValue("@powod", powod);
            cmd.Parameters.AddWithValue("@sukces", sukces);
            cmd.Parameters.AddWithValue("@akcja", akcja);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation($"Log zapisany w bazie: {typ}/{komponent}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd zapisu logu do bazy");
        }
    }


    private async Task UpdateDeviceStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string doorStatus = root.TryGetProperty("drzwi", out var d) ? d.GetString() ?? "brak" : "brak";
            string wifiStatus = root.TryGetProperty("wifi", out var w) ? w.GetString() ?? "brak" : "brak";
            string rfidStatus = root.TryGetProperty("rfid", out var r) ? r.GetString() ?? "brak" : "brak";
            string gsmStatus = root.TryGetProperty("gsm", out var g) ? g.GetString() ?? "brak" : "brak";
            string cameraStatus = root.TryGetProperty("kamera", out var k) ? k.GetString() ?? "brak" : "brak";

            using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
            await db.OpenAsync();

            // wyczyść poprzedni status
            using (var clearCmd = new MySqlCommand("DELETE FROM device_status", db))
                await clearCmd.ExecuteNonQueryAsync();

            // wstaw nowy aktualny status
            const string insertQuery = @"
            INSERT INTO device_status 
                (status_drzwi, status_polaczenia, status_rfid, status_gsm, status_kamera, data_aktualizacji)
            VALUES (@door, @wifi, @rfid, @gsm, @camera, NOW())";

            using var cmd = new MySqlCommand(insertQuery, db);
            cmd.Parameters.AddWithValue("@door", doorStatus);
            cmd.Parameters.AddWithValue("@wifi", wifiStatus);
            cmd.Parameters.AddWithValue("@rfid", rfidStatus);
            cmd.Parameters.AddWithValue("@gsm", gsmStatus);
            cmd.Parameters.AddWithValue("@camera", cameraStatus);
            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation($"Zaktualizowano status urządzenia: drzwi={doorStatus}, wifi={wifiStatus}, gsm={gsmStatus}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisu statusu urządzenia do bazy");
        }
    }


    private async Task SaveNotification(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string eventType = root.TryGetProperty("eventType", out var e)
                ? e.GetString() ?? "unknown"
                : "unknown";

            string message = root.TryGetProperty("message", out var m)
                ? m.GetString() ?? ""
                : "";

            using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
            await db.OpenAsync();

            const string query = @"
            INSERT INTO esp_notifications (event_type, message, processed, created_at)
            VALUES (@type, @msg, 0, NOW());";

            using var cmd = new MySqlCommand(query, db);
            cmd.Parameters.AddWithValue("@type", eventType);
            cmd.Parameters.AddWithValue("@msg", message);

            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation($"🔔 Zapisano powiadomienie MQTT: {eventType} ({message})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Błąd zapisu powiadomienia do bazy");
        }
    }
  private static string NormalizeUid(string uid)
    => uid?.ToUpperInvariant().Replace(":", "").Replace("-", "") ?? "";

private async Task HandleRfidAccess(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        string rawUid = doc.RootElement.GetProperty("uid").GetString() ?? "";
        string uid = NormalizeUid(rawUid);

        await using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
        await db.OpenAsync();

        const string sql = @"
            SELECT COUNT(*) 
            FROM rfid_cards 
            WHERE REPLACE(REPLACE(UPPER(uid), ':',''),'-','') = @uid
              AND aktywna = 1
              AND (data_waznosci IS NULL OR data_waznosci > NOW())
        ";
        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@uid", uid); 
        int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        if (count == 0)
        {
            //karta nieaktywna
            string ack = JsonSerializer.Serialize(new { uid = rawUid, success = false, message = "denied" });
            await PublishAsync("zamek/rfid/access/ack", ack);
            _logger.LogWarning($"RFID {uid} -> ODMOWA (brak aktywnej karty)");
            return;
        }

        //karta OK = zainicjuj 2FA
       await HandleRfidToken(JsonSerializer.Serialize(new { uid = rawUid, ttl_sec = 300 }));

        // powiedz ESP, że czekamy na 2FA (nie otwieraj zamka)
        string ackWait = JsonSerializer.Serialize(new { uid = rawUid, success = false, message = "2FA_required" });
        await PublishAsync("zamek/rfid/access/ack", ackWait);

        _logger.LogInformation($"RFID {uid} -> Karta OK, rozpoczęto proces 2FA");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd RFID access");
    }
}


    private async Task HandleRfidToken(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        string uid = doc.RootElement.GetProperty("uid").GetString() ?? "";
        int ttlSec = doc.RootElement.TryGetProperty("ttl_sec", out var ttlProp)
            ? ttlProp.GetInt32()
            : 300;

        await using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
        await db.OpenAsync();

        // 🔹 Znajdź kartę RFID i użytkownika
        const string findCardQuery = @"
            SELECT c.id AS card_id, c.user_id, u.telefon AS phone
            FROM rfid_cards c
            LEFT JOIN users u ON u.id = c.user_id
            WHERE REPLACE(REPLACE(UPPER(c.uid), ':', ''), '-', '') = @uid
              AND c.aktywna = 1
              AND (c.data_waznosci IS NULL OR c.data_waznosci > NOW())
            LIMIT 1";

        await using var findCmd = new MySqlCommand(findCardQuery, db);
        findCmd.Parameters.AddWithValue("@uid", uid);
        await using var reader = await findCmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            await PublishAsync("zamek/rfid/token/ack",
                JsonSerializer.Serialize(new { ok = false, uid, reason = "Brak aktywnej karty" }));
            return;
        }

     int cardId = reader.GetInt32(reader.GetOrdinal("card_id"));
int userId = reader.GetInt32(reader.GetOrdinal("user_id"));
        string phoneNumber = reader["phone"]?.ToString()?.Trim() ?? "";
        await reader.CloseAsync();

        if (string.IsNullOrEmpty(phoneNumber))
        {
            await PublishAsync("zamek/rfid/token/ack",
                JsonSerializer.Serialize(new { ok = false, uid, reason = "Brak telefonu" }));
            return;
        }

        // Dezaktywuj stare tokeny
        const string disableOldQuery = "UPDATE rfid_tokens SET uzyty = 1 WHERE rfid_card_id = @id AND uzyty = 0";
        await using (var disableOld = new MySqlCommand(disableOldQuery, db))
        {
            disableOld.Parameters.AddWithValue("@id", cardId);
            await disableOld.ExecuteNonQueryAsync();
        }

        // Wygeneruj 6-cyfrowy kod
        string code = new Random().Next(100000, 999999).ToString();

        // Wstaw nowy token do tabeli rfid_tokens
        const string insertQuery = @"
            INSERT INTO rfid_tokens 
                (rfid_card_id, user_id, kod_2fa, data_waznosci, uzyty, proby_bledne)
            VALUES 
                (@cardId, @userId, @code, DATE_ADD(NOW(), INTERVAL @ttl SECOND), 0, 0)";
        await using var insertCmd = new MySqlCommand(insertQuery, db);
        insertCmd.Parameters.AddWithValue("@cardId", cardId);
        insertCmd.Parameters.AddWithValue("@userId", userId);
        insertCmd.Parameters.AddWithValue("@code", code);
        insertCmd.Parameters.AddWithValue("@ttl", ttlSec);
        await insertCmd.ExecuteNonQueryAsync();

        long newTokenId = insertCmd.LastInsertedId;

        // Wyślij SMS przez MQTT (ESP -> GSM)
        string msg = $"Kod 2FA: {code} (wazny {ttlSec}s)";
        await PublishAsync("zamek/sms/send", JsonSerializer.Serialize(new { phone = phoneNumber, msg }));

        // Odpowiedź dla ESP
        string ack = JsonSerializer.Serialize(new
        {
            ok = true,
            uid,
            token_id = newTokenId,
            phone = phoneNumber,
            code,
            ttl_sec = ttlSec
        });
        await PublishAsync("zamek/rfid/token/ack", ack);

        _logger.LogInformation($"Wysłano token 2FA do {phoneNumber}: {code}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd RFID token");
    }
}

private async Task HandleRfidVerify(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        string rawUid = doc.RootElement.GetProperty("uid").GetString() ?? "";
        string uid = NormalizeUid(rawUid);
        int tokenId = doc.RootElement.GetProperty("token_id").GetInt32();
        string code = doc.RootElement.GetProperty("code").GetString() ?? "";

        await using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
        await db.OpenAsync();

        const string selectQuery = @"
            SELECT t.id, t.kod_2fa, t.data_waznosci, t.uzyty
            FROM rfid_tokens t
            JOIN rfid_cards c ON c.id = t.rfid_card_id
            WHERE t.id = @tokenId
              AND REPLACE(REPLACE(UPPER(c.uid), ':',''),'-','') = @uid
              AND c.aktywna = 1
              AND (c.data_waznosci IS NULL OR c.data_waznosci > NOW())
            LIMIT 1";
        await using var selectCmd = new MySqlCommand(selectQuery, db);
        selectCmd.Parameters.AddWithValue("@tokenId", tokenId);
        selectCmd.Parameters.AddWithValue("@uid", uid);

        using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            await PublishAsync("zamek/rfid/verify/ack",
                JsonSerializer.Serialize(new { uid = rawUid, status = "FAIL", ok = false, reason = "Brak tokenu/karty" }));
            return;
        }

        string codeFromDb = reader["kod_2fa"].ToString().Trim();
        var expiry = reader.GetDateTime(reader.GetOrdinal("data_waznosci"));
        bool used = Convert.ToBoolean(reader["uzyty"]);
        await reader.CloseAsync();

        _logger.LogInformation($"DEBUG 2FA: kodDB='{codeFromDb}', kodESP='{code}', expiry={expiry}, used={used}");

        if (used)
        {
            await PublishAsync("zamek/rfid/verify/ack",
                JsonSerializer.Serialize(new { uid = rawUid, status = "FAIL", ok = false, reason = "Token użyty" }));
            return;
        }
        if (expiry <= DateTime.Now.AddSeconds(-2))
        {
            await PublishAsync("zamek/rfid/verify/ack",
                JsonSerializer.Serialize(new { uid = rawUid, status = "FAIL", ok = false, reason = "Wygasł" }));
            return;
        }
        if (!string.Equals(codeFromDb.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            const string incQuery = "UPDATE rfid_tokens SET proby_bledne = proby_bledne + 1 WHERE id = @id";
            await using var incCmd = new MySqlCommand(incQuery, db);
            incCmd.Parameters.AddWithValue("@id", tokenId);
            await incCmd.ExecuteNonQueryAsync();

            await PublishAsync("zamek/rfid/verify/ack",
                JsonSerializer.Serialize(new { uid = rawUid, status = "FAIL", ok = false, reason = "Zły kod" }));
            return;
        }

        const string updateQuery = "UPDATE rfid_tokens SET uzyty = 1 WHERE id = @id AND uzyty = 0";
        await using var updateCmd = new MySqlCommand(updateQuery, db);
        updateCmd.Parameters.AddWithValue("@id", tokenId);
        int affected = await updateCmd.ExecuteNonQueryAsync();
        bool ok = affected > 0;

        await PublishAsync("zamek/rfid/verify/ack",
            JsonSerializer.Serialize(new { uid = rawUid, status = ok ? "OK" : "FAIL", ok }));

        _logger.LogInformation($"Weryfikacja 2FA (uid={uid}, token={tokenId}) => {(ok ? "OK" : "BŁĄD")}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd RFID verify");
    }
}


private async Task HandleSmsSendQueue()
{
    try
    {
        await using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
        await db.OpenAsync();

        const string query = @"
            SELECT 
                id, phone_number, login_code,
                GREATEST(TIMESTAMPDIFF(SECOND, NOW(), valid_until), 0) AS ttl_sec
            FROM access_codes
            WHERE sms_sent = 0 AND valid_until > NOW() AND is_blocked = 0
            ORDER BY id ASC
            LIMIT 1";

        await using var cmd = new MySqlCommand(query, db);
        await using var rdr = await cmd.ExecuteReaderAsync();

        if (!rdr.Read())
        {
            _logger.LogInformation("📭 Brak oczekujących SMS-ów do wysłania.");
            return;
        }

        // użyj indeksów kolumn
        int id = rdr.GetInt32(0);                     // kolumna: id
        string phone = rdr.GetString(1);              // kolumna: phone_number
        string code = rdr.GetString(2);               // kolumna: login_code
        int ttlSec = rdr.GetInt32(3);                 // kolumna: ttl_sec

        string payload = JsonSerializer.Serialize(new
        {
            id,
            phone,
            msg = $"Kod dostępu: {code} (ważny {ttlSec}s)"
        });

        await PublishAsync("zamek/sms/send", payload);
        _logger.LogInformation($"Wysłano przez MQTT do ESP: SMS #{id} -> {phone}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd przy HandleSmsSendQueue()");
    }
}
    private async Task HandleSmsSent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            int id = doc.RootElement.GetProperty("id").GetInt32(); //ID jako int

            await using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
            await db.OpenAsync();

            const string update = @"
            UPDATE access_codes
            SET sms_sent = 1, sent_at = NOW()
            WHERE id = @id AND sms_sent = 0";

            await using var cmd = new MySqlCommand(update, db);
            cmd.Parameters.AddWithValue("@id", id); // nie konwertuj na string!

            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
                _logger.LogInformation($"Potwierdzono wysłanie SMS (ID={id})");
            else
                _logger.LogWarning($"SMS ID={id} był już oznaczony jako wysłany.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd przy HandleSmsSent()");
        }
    }
private async Task HandleAccessCode(string json)
{
    try
    {
        _logger.LogInformation($"Odebrano MQTT access_code: {json}");
        using var doc = JsonDocument.Parse(json);
        string code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Odebrano pusty kod dostępu przez MQTT");
            await PublishAsync("zamek/access/ack", "{\"status\":\"FAIL\"}");
            return;
        }

        await using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
        await db.OpenAsync();

        const string checkQuery = @"
            SELECT id, max_uses, used_count, valid_until, is_blocked
            FROM access_codes
            WHERE login_code = @code
            ORDER BY id DESC
            LIMIT 1";

        await using var checkCmd = new MySqlCommand(checkQuery, db);
        checkCmd.Parameters.AddWithValue("@code", code);

        await using var rdr = await checkCmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync())
        {
            _logger.LogInformation($"Kod {code} nie istnieje w bazie.");
            await PublishAsync("zamek/access/ack", "{\"status\":\"FAIL\"}");
            return;
        }

        int codeId = Convert.ToInt32(rdr["id"]);
        object maxUseObj = rdr["max_uses"];
        int usedCount = Convert.ToInt32(rdr["used_count"]);
        DateTime validUntil = Convert.ToDateTime(rdr["valid_until"]);
        bool blocked = Convert.ToBoolean(rdr["is_blocked"]);
        await rdr.CloseAsync();

        // Walidacja
        if (blocked || validUntil <= DateTime.Now)
        {
            _logger.LogWarning($"Kod {code} jest zablokowany lub wygasł ({validUntil}).");
            await PublishAsync("zamek/access/ack", "{\"status\":\"FAIL\"}");
            return;
        }

        int? maxUses = maxUseObj == DBNull.Value ? null : Convert.ToInt32(maxUseObj);
        bool stillValid = !maxUses.HasValue || usedCount < maxUses.Value;

        if (!stillValid)
        {
            _logger.LogWarning($"Kod {code} przekroczył limit użyć ({usedCount}/{maxUses}).");
            await PublishAsync("zamek/access/ack", "{\"status\":\"FAIL\"}");
            return;
        }

        // Aktualizacja
        const string updateQuery = @"
            UPDATE access_codes
            SET used_count = used_count + 1, used_at = NOW()
            WHERE id = @id";

        await using var updateCmd = new MySqlCommand(updateQuery, db);
        updateCmd.Parameters.AddWithValue("@id", codeId);
        await updateCmd.ExecuteNonQueryAsync();

        _logger.LogInformation($"Kod {code} poprawny - przyznano dostęp (ID={codeId}).");
        await PublishAsync("zamek/access/ack", "{\"status\":\"OK\"}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd w HandleAccessCode()");
        await PublishAsync("zamek/access/ack", "{\"status\":\"FAIL\"}");
    }
}

}
