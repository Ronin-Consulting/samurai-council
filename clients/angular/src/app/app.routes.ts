import { Routes } from '@angular/router';
import { ChatPage } from './components/chat-page';

export const routes: Routes = [
  { path: '', component: ChatPage },
  { path: 'chat/:id', component: ChatPage },
  { path: '**', redirectTo: '' },
];
