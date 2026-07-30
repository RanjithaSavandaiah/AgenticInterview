import { Routes } from '@angular/router';
import { InterviewRoom } from './interview-room/interview-room';
import { HrDashboard } from './hr-dashboard/hr-dashboard';
import { SetupInterviewComponent } from './setup-interview/setup-interview.component';

export const routes: Routes = [
    { path: '', component: SetupInterviewComponent },
    { path: 'interview/:sessionId', component: InterviewRoom },
    { path: 'hr-dashboard', component: HrDashboard },
    { path: '**', redirectTo: '' }
];
