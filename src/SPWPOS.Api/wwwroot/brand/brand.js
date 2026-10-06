// PLANORA Brand Identity System — Interactive Logic
document.addEventListener('DOMContentLoaded', () => {
  // 1. Toast Notification for Color Copying
  const toast = document.createElement('div');
  toast.className = 'toast-notice';
  document.body.appendChild(toast);

  function showToast(message) {
    toast.textContent = message;
    toast.classList.add('show');
    setTimeout(() => toast.classList.remove('show'), 2200);
  }

  // 2. Click-to-copy on color swatches
  document.querySelectorAll('[data-copy]').forEach(card => {
    card.addEventListener('click', () => {
      const code = card.getAttribute('data-copy');
      if (code) {
        navigator.clipboard.writeText(code).then(() => {
          showToast(`Copied ${code} to clipboard`);
        }).catch(() => {
          showToast(`Color: ${code}`);
        });
      }
    });
  });

  // 3. Interactive Logo Scale Preview
  const scaleSlider = document.getElementById('logo-scale-slider');
  const scaleDisplay = document.getElementById('logo-scale-display');
  const scalePreview = document.getElementById('scalable-logo-symbol');

  if (scaleSlider && scaleDisplay && scalePreview) {
    scaleSlider.addEventListener('input', (e) => {
      const size = e.target.value;
      scaleDisplay.textContent = `${size}px`;
      scalePreview.style.width = `${size}px`;
      scalePreview.style.height = `${size}px`;
    });
  }

  // 4. Background Toggle for Logo Testing
  const bgBtns = document.querySelectorAll('[data-bg-test]');
  const testBox = document.getElementById('logo-test-stage');

  if (testBox && bgBtns.length > 0) {
    bgBtns.forEach(btn => {
      btn.addEventListener('click', () => {
        bgBtns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const bg = btn.getAttribute('data-bg-test');
        if (bg === 'white') {
          testBox.style.background = '#FFFFFF';
          testBox.style.borderColor = '#E2E8F0';
        } else if (bg === 'soft') {
          testBox.style.background = '#F7FCFF';
          testBox.style.borderColor = '#BAE6FD';
        } else if (bg === 'navy') {
          testBox.style.background = '#071A3D';
          testBox.style.borderColor = '#1E293B';
        } else if (bg === 'cyan') {
          testBox.style.background = '#E9FAFF';
          testBox.style.borderColor = '#7DD3FC';
        }
      });
    });
  }
});
