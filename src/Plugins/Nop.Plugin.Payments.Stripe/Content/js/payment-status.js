document.addEventListener('DOMContentLoaded', function () {
  const paymentIntentId = sessionStorage.getItem('StripePaymentIntent');

  if (paymentIntentId) {
    checkPaymentStatus(paymentIntentId);
  }
});

async function checkPaymentStatus(paymentIntentId) {
  try {
    const response = await fetch('/Plugins/PaymentStripe/CheckStatus', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
      },
      body: JSON.stringify({ paymentIntentId })
    });

    const result = await response.json();

    if (result.redirectUrl) {
      window.location.href = result.redirectUrl;
    } else if (result.errors.length > 0) {
      showPaymentErrors(result.errors);
    } else {
      setTimeout(() => checkPaymentStatus(paymentIntentId), 3000);
    }
  } catch (error) {
    console.error('Payment status check failed:', error);
  }
}

function showPaymentErrors(errors) {
  const errorContainer = document.getElementById('stripe-opc-errors');
  if (!errorContainer) return;

  errorContainer.querySelector('ul').innerHTML = errors.map(e => `<li>${e}</li>`).join('');
  errorContainer.style.display = 'block';
}