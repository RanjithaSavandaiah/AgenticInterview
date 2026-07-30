import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InterviewStore } from '../state/interview.store';

@Component({
  selector: 'app-hr-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './hr-dashboard.html',
  styleUrls: ['./hr-dashboard.css']
})
export class HrDashboard implements OnInit {

  public store = inject(InterviewStore);

  ngOnInit() {
    this.store.startSession('/hrhub');
  }

}
