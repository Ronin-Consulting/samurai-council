// TypeScript mirrors of the SamurAICouncil.Api JSON DTOs (snake_case; polymorphic Message by `role`).

export type ChartType =
  | 'None' | 'Bar' | 'HorizontalBar' | 'GroupedBar' | 'StackedBar'
  | 'Line' | 'Area' | 'Pie' | 'Donut' | 'Scatter' | 'Stat' | 'Table';

export interface ChartPoint {
  x: number;
  y: number;
  label?: string | null;
}

export interface ChartSeriesData {
  name: string;
  values: number[];
  points?: ChartPoint[] | null;
}

export interface StatData {
  label: string;
  value: number;
  unit?: string | null;
  caption?: string | null;
  deltaPercent?: number | null;
  sparkline?: number[] | null;
}

export interface TableData {
  columns: string[];
  rows: string[][];
}

export interface ChartRecommendation {
  type: ChartType;
  title: string;
  labels: string[];
  series: ChartSeriesData[];
  xAxisLabel?: string | null;
  yAxisLabel?: string | null;
  stats?: StatData[] | null;
  table?: TableData | null;
}

export interface ToolUsage {
  tool_name: string;
  input: string;
  output: string;
  chart?: ChartRecommendation | null;
}

export interface Stage1Response {
  model: string;
  response: string;
  tool_usages: ToolUsage[];
}

export interface Stage2Ranking {
  model: string;
  ranking: string;
  parsed_ranking: string[];
}

export interface Stage3Response {
  model: string;
  response: string;
  tool_usages: ToolUsage[];
  chart?: ChartRecommendation | null;
}

export interface AggregateRanking {
  model: string;
  average_rank: number;
  rankings_count: number;
}

export interface CouncilMetadata {
  label_to_model: Record<string, string>;
  aggregate_rankings: AggregateRanking[];
  tools_enabled: boolean;
  total_tool_calls: number;
}

export interface LoadingState {
  stage1: boolean;
  stage2: boolean;
  stage3: boolean;
}

export interface UserMessage {
  role: 'user';
  content: string;
}

export interface AssistantMessage {
  role: 'assistant';
  stage1?: Stage1Response[] | null;
  stage2?: Stage2Ranking[] | null;
  stage3?: Stage3Response | null;
  metadata?: CouncilMetadata | null;
  loading?: LoadingState | null;
}

export type Message = UserMessage | AssistantMessage;

export interface Conversation {
  id: string;
  created_at: string;
  title: string;
  messages: Message[];
}

export interface ConversationSummary {
  id: string;
  title: string;
  created_at: string;
  updated_at?: string | null;
}
