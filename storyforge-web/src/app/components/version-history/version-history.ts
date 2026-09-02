import { Component, input, output } from '@angular/core';
import { StoryVersion } from '../../models/story.models';

@Component({
  selector: 'app-version-history',
  templateUrl: './version-history.html',
  styleUrl: './version-history.css'
})
export class VersionHistory {
  readonly versions = input.required<StoryVersion[]>();
  readonly activeVersionId = input<string | undefined>();

  readonly select = output<string>();
}
