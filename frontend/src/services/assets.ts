const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

export function resolveApiAssetUrl(value?: string): string {
  if (!value) {
    return '';
  }

  if (/^(https?:|data:)/i.test(value)) {
    return value;
  }

  return new URL(value, `${API_BASE_URL.replace(/\/$/, '')}/`).toString();
}
