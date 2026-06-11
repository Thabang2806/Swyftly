import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function getApiErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Something went wrong. Please try again.';
  }

  if (error.status === 0) {
    return 'Unable to reach Mabuntle right now. Please check that the API is running.';
  }

  const problem = typeof error.error === 'object' && error.error !== null
    ? error.error as ProblemDetails
    : null;

  const validationMessage = problem?.errors
    ? Object.values(problem.errors).flat().find(Boolean)
    : null;

  return validationMessage
    ?? problem?.detail
    ?? problem?.title
    ?? defaultStatusMessage(error.status);
}

function defaultStatusMessage(status: number): string {
  if (status === 401) {
    return 'The email address or password is incorrect.';
  }

  if (status === 403) {
    return 'You do not have access to that area.';
  }

  return 'Something went wrong. Please try again.';
}
