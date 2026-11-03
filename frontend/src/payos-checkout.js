const sdkUrl =
  "https://cdn.payos.vn/payos-checkout/v1/stable/payos-initialize.js";
let loading;

export function payOsReturnParameter(value) {
  try {
    const url = new URL(value);
    if (url.protocol !== "https:" || url.username || url.password) return null;
    // The SDK interpolates this value without escaping it. Preserve all query
    // parameters/fragments so payOS receives the exact server-signed return URL.
    return encodeURIComponent(url.href);
  } catch {
    return null;
  }
}

export function safePayOsCheckout(value) {
  try {
    const url = new URL(value);
    return url.protocol === "https:" &&
      url.hostname === "pay.payos.vn" &&
      !url.username &&
      !url.password &&
      !url.port
      ? url.href
      : null;
  } catch {
    return null;
  }
}

// payOS requires its official SDK to be loaded directly from their CDN.
export function loadPayOsCheckout() {
  if (window.PayOSCheckout?.usePayOS)
    return Promise.resolve(window.PayOSCheckout);
  if (loading) return loading;
  loading = new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = sdkUrl;
    script.async = true;
    const timer = setTimeout(() => fail(), 15000);
    function fail() {
      clearTimeout(timer);
      script.onload = script.onerror = null;
      script.remove();
      loading = undefined;
      reject(
        new Error(
          "Chưa tải được thanh toán payOS. Kiểm tra kết nối và thử lại.",
        ),
      );
    }
    script.onerror = fail;
    script.onload = () => {
      if (!window.PayOSCheckout?.usePayOS) return fail();
      clearTimeout(timer);
      script.onload = script.onerror = null;
      resolve(window.PayOSCheckout);
    };
    document.head.appendChild(script);
  });
  return loading;
}
