export class PromptOneError extends Error {
  constructor(message: string, options?: ErrorOptions) {
    super(message, options);
    this.name = new.target.name;
  }
}

/** Request rejected locally, before any network traffic. */
export class PromptOneValidationError extends PromptOneError {}

/** A non-2xx response. Includes JSON error details when available. */
export class PromptOneAPIError extends PromptOneError {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code: string,
    public readonly details: unknown,
  ) {
    super(message);
  }
}

export class PromptOneConnectionError extends PromptOneError {}
export class PromptOneTimeoutError extends PromptOneError {}
export class PromptOneAbortError extends PromptOneError {}
export class PromptOneResponseError extends PromptOneError {}
