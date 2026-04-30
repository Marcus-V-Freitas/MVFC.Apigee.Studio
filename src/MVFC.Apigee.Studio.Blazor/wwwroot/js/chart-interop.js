window.renderAnalyticsChart = (canvasId, data) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    // Destruir gráfico anterior se existir (para evitar sobreposição no refresh)
    const existingChart = Chart.getChart(canvasId);
    if (existingChart) {
        existingChart.destroy();
    }

    // O emulador retorna algo como: [{"timestamp": "...", "proxy": "...", "response_code": 200, "client_received_start_timestamp": ...}]
    // Vamos agrupar por minuto ou apenas mostrar as últimas 20 requisições
    
    const labels = data.map(d => new Date(d.client_received_start_timestamp / 1000).toLocaleTimeString());
    const durations = data.map(d => d.target_response_end_timestamp ? (d.target_response_end_timestamp - d.client_received_start_timestamp) / 1000 : 0);

    new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Latência (ms)',
                data: durations,
                borderColor: '#56c172',
                backgroundColor: 'rgba(86, 193, 114, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    grid: { color: 'rgba(255, 255, 255, 0.1)' },
                    ticks: { color: '#8b949e' }
                },
                x: {
                    grid: { display: false },
                    ticks: { color: '#8b949e', maxRotation: 45, minRotation: 45 }
                }
            },
            plugins: {
                legend: { display: false }
            }
        }
    });
}
