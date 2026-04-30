window.downloadFile = (fileName, contentString) => {
    const blob = new Blob([contentString], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? 'trace.json';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);
}
