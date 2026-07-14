import { Component, input } from '@angular/core';
import { TableData } from '../models';

/** Tabular result. Used for ChartType 'Table' and as the accessible table-view fallback. */
@Component({
  selector: 'app-data-table',
  template: `
    <div class="my-3 max-h-96 overflow-auto rounded-xl border border-neutral-200 dark:border-neutral-700">
      <table class="w-full text-sm">
        <thead class="sticky top-0 bg-neutral-100 dark:bg-neutral-800">
          <tr>
            @for (col of table().columns; track col) {
              <th class="px-3 py-2 text-left font-medium whitespace-nowrap">{{ col }}</th>
            }
          </tr>
        </thead>
        <tbody>
          @for (row of table().rows; track $index) {
            <tr class="border-t border-neutral-200 dark:border-neutral-800">
              @for (cell of row; track $index) {
                <td class="px-3 py-1.5 tabular-nums whitespace-nowrap">{{ cell }}</td>
              }
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class DataTable {
  table = input.required<TableData>();
}
