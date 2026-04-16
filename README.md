# Electronic Smart Lock

A full-stack smart lock system built with **ESP32** and a **.NET web application**, communicating over **MQTT**. The web app is hosted on a **VPS** and allows remote lock management from any browser.

---

## Project Structure

```
Electronic-Smart-Lock/
├── src/                  # ESP32 firmware (PlatformIO)
├── platformio.ini
├── WebApp/               # .NET web application
│   ├── WebApp.sln
│   └── WebApp/
│       ├── Program.cs
│       └── ...
└── .gitignore
```

---

## How It Works

```
[ Browser ] ──── HTTPS ────► [ .NET Web App on VPS ]
                                        │
                                      MQTT
                                        │
                               [ ESP32 Smart Lock ]
```

1. User logs into the web app hosted on VPS
2. Web app sends commands via **MQTT broker**
3. ESP32 receives the command and locks/unlocks the door
4. ESP32 sends status back to the web app via MQTT

---

## Web Application (.NET)

The web app provides a browser-based interface for managing the smart lock remotely.

**Features:**
- Remote lock / unlock control
- User access management
- Entry history and activity monitoring
- Real-time lock status

**Tech stack:**
- ASP.NET Core
- MQTT client for ESP32 communication
- Hosted on VPS

---

## irmware (ESP32 / PlatformIO)

The ESP32 controls the physical lock mechanism and communicates with the web app via MQTT.

**Features:**
- Connects to WiFi and MQTT broker
- Receives lock/unlock commands
- Reports lock status back to the server

---

## Getting Started

### Firmware

1. Install [PlatformIO](https://platformio.org/)
2. Clone the repository
3. Open the project in VS Code with PlatformIO extension
4. Configure WiFi and MQTT settings in `src/config.h`
5. Upload to ESP32

```bash
git clone https://github.com/pawelpuczkowski/Electronic-Smart-Lock.git
cd Electronic-Smart-Lock
pio run --target upload
```

### Web Application

1. Navigate to the `WebApp/` folder
2. Configure MQTT broker settings in `appsettings.json`
3. Run the application

```bash
cd WebApp
dotnet restore
dotnet run
```

---

## Requirements

| Component | Details |
|-----------|---------|
| Microcontroller | ESP32 |
| Firmware IDE | PlatformIO + VS Code |
| Backend | .NET 8+ |
| Communication | MQTT |
| Hosting | VPS (Linux) |

---

## License

MIT License - free to use and modify.
