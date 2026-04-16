// Przygotowanie danych do wykresów Chart.js
const dataZdarzenia = {
    labels: modelData.labelList,
    datasets: [{
        label: 'Liczba zdarzeń dziennie',
        data: modelData.valueList,
        backgroundColor: 'rgba(0, 255, 255, 0.7)'
    }]
};

const dataBledy = {
    labels: modelData.bledyLabels,
    datasets: [{
        label: 'Błędne zdarzenia',
        data: modelData.bledyValues,
        backgroundColor: 'rgba(255, 100, 100, 0.7)'
    }]
};

const dataKomponenty = {
    labels: modelData.komponentyLabels,
    datasets: [{
        label: 'Zdarzenia wg komponentu',
        data: modelData.komponentyValues,
        backgroundColor: 'rgba(100, 200, 255, 0.7)'
    }]
};

const dataZdjecia = {
    labels: modelData.zdjeciaLabels,
    datasets: [{
        label: 'Zdjęcia dziennie',
        data: modelData.zdjeciaValues,
        backgroundColor: 'rgba(180, 255, 100, 0.7)'
    }]
};

const dataTypy = {
    labels: modelData.typyLabels,
    datasets: [{
        label: 'Typy zdarzeń',
        data: modelData.typyValues,
        backgroundColor: 'rgba(255, 200, 100, 0.7)'
    }]
};

const dataGodziny = {
    labels: modelData.godzinyLabels,
    datasets: [{
        label: 'Aktywność wg godziny',
        data: modelData.godzinyValues,
        backgroundColor: 'rgba(100, 255, 180, 0.7)'
    }]
};

// Inicjalizacja wykresów
const chart1 = new Chart(document.getElementById('chartZdarzenia'), { type: 'bar', data: dataZdarzenia });
const chart2 = new Chart(document.getElementById('chartBledy'), { type: 'bar', data: dataBledy });
const chart3 = new Chart(document.getElementById('chartKomponenty'), { type: 'bar', data: dataKomponenty });
const chart4 = new Chart(document.getElementById('chartZdjecia'), { type: 'bar', data: dataZdjecia });
const chart5 = new Chart(document.getElementById('chartTypyZdarzen'), { type: 'bar', data: dataTypy });
const chart6 = new Chart(document.getElementById('chartGodziny'), { type: 'bar', data: dataGodziny });

// Przypisanie wykresów do canvasów
document.getElementById('chartZdarzenia').chart = chart1;
document.getElementById('chartBledy').chart = chart2;
document.getElementById('chartKomponenty').chart = chart3;
document.getElementById('chartZdjecia').chart = chart4;
document.getElementById('chartTypyZdarzen').chart = chart5;
document.getElementById('chartGodziny').chart = chart6;

// Obsługa przełączania zakładek (Chart.js resize)
document.querySelectorAll('button[data-bs-toggle="tab"]').forEach(btn => {
    btn.addEventListener('shown.bs.tab', function (e) {
        const targetId = e.target.getAttribute("data-bs-target");
        const canvas = document.querySelector(`${targetId} canvas`);
        if (canvas && canvas.chart) {
            canvas.chart.resize();
        }
    });
});

// Obsługa przycisku "Zrób zdjęcie"
document.getElementById("zrobZdjecieBtn").addEventListener("click", async () => {
    const status = document.getElementById("zdjecieStatus");
    status.textContent = "Wysyłanie żądania...";

    try {
        const response = await fetch("?handler=ZrobZdjecie", {
            method: "POST"
        });

        if (!response.ok) throw new Error("Błąd sieci");

        status.textContent = "Zrobiono zdjęcie! Pobieranie...";

        const zdjResponse = await fetch("?handler=OstatnieZdjecieJson");
        const json = await zdjResponse.json();

        if (json && json.url) {
            document.getElementById("najnowszeZdjecie").src = json.url + "?t=" + new Date().getTime(); // cache bust
            status.textContent = "Zdjęcie odświeżone!";
        } else {
            status.textContent = "Nie udało się pobrać zdjęcia.";
        }
    } catch (err) {
        status.textContent = "Oczekiwanie na zdjęcie";
    }
});

// Obsługa powiększenia zdjęcia (lightbox)
function powiekszZdjecie(src) {
    const lightbox = document.getElementById('lightbox');
    document.getElementById('lightbox-img').src = src;
    lightbox.style.display = 'flex';
    lightbox.classList.add('d-flex');
}
function zamknijLightbox() {
    const lightbox = document.getElementById('lightbox');
    lightbox.style.display = 'none';
    lightbox.classList.remove('d-flex');
}

document.getElementById('lightbox').addEventListener('click', function (e) {
    if (e.target.id === 'lightbox') {
        zamknijLightbox();
    }
});


