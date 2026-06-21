import axios from 'axios';

interface ErrorPayload {
  message?: string;
  title?: string;
}

export function getApiErrorMessage(error: unknown, fallback: string) {
  if (!axios.isAxiosError<ErrorPayload>(error)) {
    return fallback;
  }

  return error.response?.data?.message ?? error.response?.data?.title ?? fallback;
}
