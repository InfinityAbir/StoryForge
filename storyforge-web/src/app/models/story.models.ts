export type GenerationMode = 'initial' | 'regenerate' | 'feedback';
export type InputLanguage = 'auto' | 'english' | 'bangla';
export type OutputLanguage = 'same' | 'english' | 'bangla';
export type StoryLength = 'short' | 'medium' | 'long';

export interface StoryDna {
  genre: string[];
  themes: string[];
  tone: string[];
  protagonistArchetype: string;
  centralConflict: string;
  emotionalArc: string[];
  structure: string[];
  endingType: string;
}

export interface GenerateStoryRequest {
  mode: GenerationMode;
  story?: string | null;
  storyDna?: StoryDna | null;
  previousStory?: string | null;
  feedback?: string | null;
  inputLanguage: InputLanguage;
  outputLanguage: OutputLanguage;
  length: StoryLength;
}

export interface GenerateStoryResponse {
  title: string;
  story: string;
  storyDna: StoryDna;
}

export interface ApiErrorResponse {
  error: {
    code: string;
    message: string;
  };
}

export interface StoryVersion {
  id: string;
  title: string;
  story: string;
  storyDna: StoryDna;
  feedback?: string;
  createdAt: string;
}
