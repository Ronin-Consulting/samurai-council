import { Component, computed, inject, input, signal } from '@angular/core';
import { MarkdownComponent } from 'ngx-markdown';
import { AssistantMessage } from '../models';
import { VisualDisplay } from './visual-display';
import { StudioDisplay } from './studio/studio-display';
import { ApiService } from '../services/api.service';
import { ViewModeService } from '../services/view-mode.service';
import { deAnonymize, getProviderColor, getProviderInitial, getShortModelName } from '../util';

type Tab = 'final' | 'responses' | 'rankings';

@Component({
  selector: 'app-assistant-message',
  imports: [MarkdownComponent, VisualDisplay, StudioDisplay],
  template: `
    @if (message(); as m) {
      @if (m.loading; as l) {
        <!-- Progressive loading indicator -->
        <div class="rounded-xl border border-neutral-200 dark:border-neutral-700 p-4">
          <div class="flex items-center gap-3 text-sm text-neutral-500">
            <span class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-red-600 border-t-transparent"></span>
            <span>{{ loadingLabel() }}</span>
          </div>
          <div class="mt-3 flex gap-2 text-xs">
            <span [class]="stepClass(1)">1 · Responses</span>
            <span [class]="stepClass(2)">2 · Review</span>
            <span [class]="stepClass(3)">3 · Synthesis</span>
          </div>
        </div>
      } @else {
        <div class="rounded-xl border border-neutral-200 dark:border-neutral-700 overflow-hidden">
          <!-- Tabs -->
          <div class="flex border-b border-neutral-200 dark:border-neutral-700 text-sm">
            <button (click)="tab.set('final')" [class]="tabClass('final')" [disabled]="!m.stage3">Final Answer</button>
            <button (click)="tab.set('responses')" [class]="tabClass('responses')">Responses ({{ m.stage1?.length ?? 0 }})</button>
            <button (click)="tab.set('rankings')" [class]="tabClass('rankings')">Rankings ({{ m.stage2?.length ?? 0 }})</button>
          </div>

          <div class="p-4">
            <!-- FINAL -->
            @if (tab() === 'final' && m.stage3; as s3) {
              <div class="flex items-center justify-between mb-2">
                <span class="text-xs rounded bg-red-600 text-white px-2 py-0.5">Chairman: {{ short(s3.model) }}</span>
                @if (conversationId()) {
                  <span class="flex gap-2">
                    <a [href]="pdfUrl()" class="text-xs px-2 py-1 rounded border border-neutral-300 dark:border-neutral-600 hover:bg-neutral-100 dark:hover:bg-neutral-800">PDF</a>
                    <a [href]="xlsxUrl()" class="text-xs px-2 py-1 rounded border border-neutral-300 dark:border-neutral-600 hover:bg-neutral-100 dark:hover:bg-neutral-800">Excel</a>
                  </span>
                }
              </div>
              <markdown class="prose-sm" [data]="s3.response"></markdown>
              @if (viewMode.mode() === 'studio') {
                @if (s3.studio_chart) { <app-studio-display [recommendation]="s3.studio_chart" /> }
              } @else {
                @if (s3.chart) { <app-visual-display [recommendation]="s3.chart" /> }
              }
            }

            <!-- RESPONSES -->
            @if (tab() === 'responses') {
              @for (r of m.stage1 ?? []; track r.model) {
                <div class="mb-4">
                  <div class="flex items-center gap-2 mb-1">
                    <span class="inline-flex h-6 w-6 items-center justify-center rounded-full text-white text-xs {{ color(r.model) }}">{{ initial(r.model) }}</span>
                    <span class="font-medium text-sm">{{ short(r.model) }}</span>
                    @if (r.tool_usages.length) { <span class="text-xs text-blue-500">🔧 tools</span> }
                  </div>
                  <markdown class="prose-sm" [data]="r.response"></markdown>
                </div>
              }
            }

            <!-- RANKINGS -->
            @if (tab() === 'rankings') {
              @if (m.metadata?.aggregate_rankings?.length) {
                <table class="w-full text-sm mb-4">
                  <thead><tr class="text-left text-neutral-500"><th class="py-1">#</th><th>Model</th><th>Avg Rank</th></tr></thead>
                  <tbody>
                    @for (a of m.metadata!.aggregate_rankings; track a.model; let i = $index) {
                      <tr class="border-t border-neutral-200 dark:border-neutral-700">
                        <td class="py-1">{{ i + 1 }}</td>
                        <td>{{ short(a.model) }}</td>
                        <td>{{ a.average_rank.toFixed(2) }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
              @for (rk of m.stage2 ?? []; track rk.model) {
                <div class="mb-3">
                  <div class="font-medium text-sm mb-1">{{ short(rk.model) }}</div>
                  <markdown class="prose-sm text-neutral-600 dark:text-neutral-300" [data]="deanon(rk.ranking, m.metadata?.label_to_model)"></markdown>
                </div>
              }
            }
          </div>
        </div>
      }
    }
  `,
})
export class AssistantMessagePanel {
  private api = inject(ApiService);
  viewMode = inject(ViewModeService);
  message = input.required<AssistantMessage>();
  query = input<string>('');
  conversationId = input<string>('');

  tab = signal<Tab>('final');

  short = getShortModelName;
  color = getProviderColor;
  initial = getProviderInitial;
  deanon = deAnonymize;

  pdfUrl = computed(() => this.api.exportUrl(this.conversationId(), 'pdf'));
  xlsxUrl = computed(() => this.api.exportUrl(this.conversationId(), 'xlsx'));

  loadingLabel = computed(() => {
    const l = this.message().loading;
    if (l?.stage3) return 'Synthesizing the final answer…';
    if (l?.stage2) return 'Peer-reviewing responses…';
    return 'Collecting responses from the council…';
  });

  private currentStep = computed(() => {
    const l = this.message().loading;
    if (l?.stage3) return 3;
    if (l?.stage2) return 2;
    return 1;
  });

  stepClass(step: number): string {
    const active = this.currentStep();
    const base = 'rounded px-2 py-0.5 ';
    if (step < active) return base + 'bg-green-600/20 text-green-600';
    if (step === active) return base + 'bg-red-600/20 text-red-600';
    return base + 'bg-neutral-200 dark:bg-neutral-700 text-neutral-500';
  }

  tabClass(t: Tab): string {
    const on = this.tab() === t;
    return 'px-4 py-2 ' + (on
      ? 'border-b-2 border-red-600 text-red-600 font-medium'
      : 'text-neutral-500 hover:text-neutral-800 dark:hover:text-neutral-200');
  }
}
