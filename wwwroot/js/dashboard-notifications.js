document.addEventListener("DOMContentLoaded", () => {   

    // Elementy w HTML
    const notifList = document.getElementById('notifList');           // lista powiadomień w navbarze
    const notifCount = document.getElementById('notifCount');         // licznik (dymek z ilością)
    const dashNotifList = document.getElementById("dashNotifList");   // lista powiadomień w dashboardzie

    // Główna funkcja – pobiera najnowsze powiadomienia z backendu
    async function loadNotifications() {
        try {
            const res = await fetch("/Notifications?handler=Latest");
            if (!res.ok) throw new Error("HTTP " + res.status);
            const data = await res.json();

            // Navbar
            if (notifList && notifCount) {
                if (!data || data.length === 0) {
                    notifList.innerHTML = `<div class="text-center text-muted small py-3">Brak powiadomień.</div>`;
                    notifCount.style.display = "none";
                } else {
                    // Liczba nieprzeczytanych
                    const newCount = data.filter(p => !p.Seen && !p.seen).length;
                    notifCount.textContent = newCount;
                    notifCount.style.display = newCount > 0 ? "inline-block" : "none";

                    // Generowanie listy powiadomień
                    notifList.innerHTML = data.map(p => {
                        const label = formatLabel(p.EventType || p.eventType);
                        const msg = p.Message || p.message || "Nowe zdarzenie";
                        const time = p.Time || p.time || "";

                        return `
                            <button class="list-group-item bg-dark small border-bottom border-secondary text-start notif-item ${p.Seen || p.seen ? 'text-secondary' : 'text-light'}"
                                    data-id="${p.Id || p.id}">
                                <div class="d-flex justify-content-between">
                                    <strong>${label}</strong>
                                    <small class="text-muted">${time}</small>
                                </div>
                                <div>${msg}</div>
                            </button>
                        `;
                    }).join("");

                    // Kliknięcie - oznaczenie jako przeczytane
                    document.querySelectorAll(".notif-item").forEach(btn => {
                        btn.addEventListener("click", async () => {
                            const id = btn.getAttribute("data-id");
                            btn.classList.remove("text-light");
                            btn.classList.add("text-secondary");

                            await markAsSeen(id); // aktualizacja na backendzie
                            btn.remove();

                            // aktualizacja liczby pozostałych
                            const remaining = document.querySelectorAll(".notif-item.text-light").length;
                            notifCount.textContent = remaining;
                            notifCount.style.display = remaining > 0 ? "inline-block" : "none";

                            if (remaining === 0)
                                notifList.innerHTML = `<div class="text-center text-muted small py-3">Brak powiadomień.</div>`;
                        });
                    });
                }
            }

            // Dashboard
            if (dashNotifList) {
                if (!data || data.length === 0) {
                    dashNotifList.innerHTML = `<div class="text-center text-muted small py-3">Brak powiadomień.</div>`;
                } else {
                    // Tworzenie listy powiadomień
                    dashNotifList.innerHTML = data.map(p => {
                        const label = formatLabel(p.EventType || p.eventType);
                        const msg = p.Message || p.message || "Nowe zdarzenie";
                        const time = p.Time || p.time || "";
                        const isSeen = p.Seen || p.seen;

                        return `
                            <li class="list-group-item bg-dark small border-bottom border-secondary text-start dash-notif-item ${isSeen ? 'text-secondary' : 'text-light'}"
                                data-id="${p.Id || p.id}">
                                <div class="d-flex justify-content-between align-items-start">
                                    <span><strong>${label}</strong>: ${msg}</span>
                                    <small class="text-muted">${time}</small>
                                </div>
                            </li>
                        `;
                    }).join("");

                    // Kliknięcie powiadomienia = oznaczenie + animacja znikania
                    document.querySelectorAll(".dash-notif-item").forEach(li => {
                        li.addEventListener("click", async () => {
                            const id = li.getAttribute("data-id");
                            li.classList.remove("text-light");
                            li.classList.add("text-secondary");

                            await markAsSeen(id);

                            // Efekt zanikania
                            li.classList.add("fade-out");
                            setTimeout(() => li.remove(), 300);

                            const remaining = document.querySelectorAll(".dash-notif-item.text-light").length;
                            if (remaining === 0)
                                dashNotifList.innerHTML = `<div class="text-center text-muted small py-3">Brak powiadomień.</div>`;
                        });
                    });
                }
            }

        } catch (err) {
            console.error("Błąd ładowania powiadomień:", err);
        }
    }

    // Oznacz powiadomienie jako przeczytane (POST do backendu)
    async function markAsSeen(id) {
        try {
            await fetch("/Notifications?handler=MarkSeen", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(parseInt(id))
            });
        } catch (err) {
            console.error("Błąd oznaczania jako przeczytane:", err);
        }
    }

    // Dopasowanie nazw zdarzeń
    function formatLabel(eventType) {
        switch (eventType) {
            case "doorbell_pressed": return "Ktoś dzwoni";
            case "camera_photo": return "Zdjęcie z kamery";
            case "door_remote_opened": return "Drzwi otwarte zdalnie";
            case "door_opened": return "Drzwi otwarte lokalnie";
            case "access_denied": return "Odmowa dostępu";
            case "code_generated": return "Wygenerowano kod";
            default: return "Zdarzenie systemowe";
        }
    }

    // Odświeżanie listy co 10 sekund
    loadNotifications();
    setInterval(loadNotifications, 10000);
});
