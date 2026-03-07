document.addEventListener('DOMContentLoaded', () => {
  const vccUrlText = document.querySelector('.url');
  if (!vccUrlText) {
    return;
  }

  vccUrlText.addEventListener('click', async () => {
    try {
      await navigator.clipboard.writeText(vccUrlText.textContent.replace('VCC URL: ', ''));
      vccUrlText.dataset.copied = 'true';
      setTimeout(() => {
        delete vccUrlText.dataset.copied;
      }, 1200);
    } catch {
      // Clipboard access can fail outside secure contexts.
    }
  });
});
