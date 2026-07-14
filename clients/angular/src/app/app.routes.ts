import { Routes } from '@angular/router';
import { ChatPage } from './components/chat-page';
import { GalleryPage } from './components/gallery-page';

export const routes: Routes = [
  { path: '', component: ChatPage },
  { path: 'chat/:id', component: ChatPage },
  { path: 'gallery', component: GalleryPage },
  { path: '**', redirectTo: '' },
];
