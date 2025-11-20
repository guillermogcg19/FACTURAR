window.crearGraficaBar = function (idCanvas, etiquetas, valores, etiqueta) {
    const ctx = document.getElementById(idCanvas).getContext('2d');
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: etiquetas,
            datasets: [{
                label: etiqueta,
                data: valores,
                backgroundColor: '#4e9cff',
                borderColor: '#1e7be4',
                borderWidth: 1
            }]
        }
    });
}
