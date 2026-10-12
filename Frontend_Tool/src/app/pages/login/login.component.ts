import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
@Component({ selector: 'app-login', standalone: true, imports: [FormsModule], templateUrl: './login.component.html', styleUrl: './login.component.css' })
export class LoginComponent {
  userName = '';
  password = '';
  registerMode = false;
  showPassword = false;
  isSubmitting = false;
  error = '';

  constructor(private readonly auth: AuthService, private readonly router: Router) {}

  submit() {
    if (this.isSubmitting) return;
    this.error = '';
    this.isSubmitting = true;
    const request = this.registerMode ? this.auth.register(this.userName, this.password) : this.auth.login(this.userName, this.password);
    request.subscribe({
      next: () => void this.router.navigate(['/dashboard']),
      error: error => { this.isSubmitting = false; this.error = error.error?.message ?? (this.registerMode ? 'Registration failed.' : 'Login failed.'); }
    });
  }
}
