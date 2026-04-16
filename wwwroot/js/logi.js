// Szukanie w tabeli po dowolnym tekście
const searchInput = document.querySelector('#searchInput');
const rows = document.querySelectorAll('tbody tr');

if (searchInput) {
    searchInput.addEventListener('input', () => {
        const query = searchInput.value.toLowerCase();
        rows.forEach((row) => {
            const rowText = row.textContent.toLowerCase();
            row.classList.toggle('d-none', !rowText.includes(query));
        });
    });
}

// Sortowanie kolumn w tabeli
const headers = document.querySelectorAll('thead th.sortable');
let currentSort = {
    column: null,
    ascending: true
};

headers.forEach((header) => {
    header.addEventListener('click', () => {
        const sortField = header.dataset.sort;
        let direction = 'asc';

        if (currentSort.column === sortField) {
            currentSort.ascending = !currentSort.ascending;
        } else {
            currentSort.column = sortField;
            currentSort.ascending = true;
        }

        direction = currentSort.ascending ? 'asc' : 'desc';
        const url = new URL(window.location.href);
        url.searchParams.set('Sortuj', sortField);
        url.searchParams.set('Kierunek', direction);
        window.location.href = url.toString();
    });
});
