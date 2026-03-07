document.addEventListener('DOMContentLoaded', () => {
  const repoUrlText = document.getElementById('repoUrlText');
  const addToVccButton = document.getElementById('addToVccButton');
  const copyRepoUrlButton = document.getElementById('copyRepoUrlButton');
  const copyStatus = document.getElementById('copyStatus');
  const addToVccButtons = document.querySelectorAll('.add-to-vcc-button');

  if (!repoUrlText) {
    return;
  }

  const repositoryUrl = repoUrlText.textContent.trim();
  const vccAddRepoUrl = `vcc://vpm/addRepo?url=${encodeURIComponent(repositoryUrl)}`;

  const setStatus = message => {
    if (!copyStatus) {
      return;
    }

    copyStatus.textContent = message;
    if (message) {
      window.setTimeout(() => {
        if (copyStatus.textContent === message) {
          copyStatus.textContent = '';
        }
      }, 1600);
    }
  };

  const copyRepositoryUrl = async () => {
    try {
      await navigator.clipboard.writeText(repositoryUrl);
      setStatus('Repository URL copied.');
    } catch {
      setStatus('Copy failed. Please copy the URL manually.');
    }
  };

  if (addToVccButton) {
    addToVccButton.addEventListener('click', () => {
      window.location.assign(vccAddRepoUrl);
    });
  }

  if (copyRepoUrlButton) {
    copyRepoUrlButton.addEventListener('click', () => {
      copyRepositoryUrl();
    });
  }

  repoUrlText.addEventListener('click', () => {
    copyRepositoryUrl();
  });

  addToVccButtons.forEach(button => {
    button.addEventListener('click', () => {
      window.location.assign(vccAddRepoUrl);
    });
  });
});
