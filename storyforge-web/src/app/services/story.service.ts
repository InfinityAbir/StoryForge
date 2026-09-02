import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, throwError, timeout } from 'rxjs';
import { ApiErrorResponse, GenerateStoryRequest, GenerateStoryResponse } from '../models/story.models';

const REQUEST_TIMEOUT_MS = 90_000;

@Injectable({ providedIn: 'root' })
export class StoryService {
  private readonly apiUrl = '/api/story/generate';

  constructor(private readonly http: HttpClient) {}

  generate(request: GenerateStoryRequest): Observable<GenerateStoryResponse> {
    return this.http.post<GenerateStoryResponse>(this.apiUrl, request).pipe(
      timeout(REQUEST_TIMEOUT_MS),
      catchError((err) => throwError(() => this.toFriendlyError(err)))
    );
  }

  private toFriendlyError(err: unknown): Error {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) {
        return new Error("We couldn't reach StoryForge. Check your connection and try again.");
      }
      if (err.status === 429) {
        return new Error("You're generating stories a little too quickly. Please try again in a moment.");
      }
      const body = err.error as ApiErrorResponse | undefined;
      if (body?.error?.code === 'PROVIDER_REFUSAL') {
        return new Error("StoryForge couldn't generate this request with the current AI model. Try changing the direction and try again.");
      }
      if (body?.error?.code === 'GENERATION_TIMEOUT') {
        return new Error('The story took too long to generate. Please try again.');
      }
      if (body?.error?.message) {
        return new Error(body.error.message);
      }
      return new Error('Story generation failed. Please try again.');
    }
    if (err && typeof err === 'object' && 'name' in err && (err as { name: string }).name === 'TimeoutError') {
      return new Error('The story took too long to generate. Please try again.');
    }
    return new Error('Story generation failed. Please try again.');
  }
}
