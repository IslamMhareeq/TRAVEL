// Register Form JavaScript - TRAVEL
const registerApp = {
    togglePassword: function (fieldId) {
        const field = document.getElementById(fieldId);
        const buttonId = fieldId === 'password' ? 'togglePassword' : 'toggleConfirmPassword';
        const button = document.getElementById(buttonId);
        const icon = button.querySelector('i');

        if (field.type === 'password') {
            field.type = 'text';
            icon.classList.remove('fa-eye');
            icon.classList.add('fa-eye-slash');
        } else {
            field.type = 'password';
            icon.classList.remove('fa-eye-slash');
            icon.classList.add('fa-eye');
        }
    },

    checkPasswordStrength: function (password) {
        const bars = document.querySelectorAll('#passwordStrength .strength-bar');
        const strengthText = document.getElementById('strengthText');
        let strength = 0;

        if (!password) {
            bars.forEach(bar => {
                bar.classList.remove('weak', 'fair', 'good', 'strong');
            });
            strengthText.textContent = '';
            strengthText.className = 'strength-text';
            return;
        }

        if (password.length >= 6) strength++;
        if (password.length >= 10) strength++;
        if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[^a-zA-Z0-9]/.test(password)) strength++;

        const barCount = Math.min(strength, 4);
        const strengthLevels = ['weak', 'fair', 'good', 'strong'];
        const strengthLabels = ['Weak', 'Fair', 'Good', 'Strong'];

        bars.forEach((bar, index) => {
            bar.classList.remove('weak', 'fair', 'good', 'strong');
            if (index < barCount) {
                bar.classList.add(strengthLevels[barCount - 1]);
            }
        });

        strengthText.textContent = strengthLabels[barCount - 1] || '';
        strengthText.className = 'strength-text ' + (strengthLevels[barCount - 1] || '');
    },

    isValidEmail: function (email) {
        const pattern = /^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
        return pattern.test(email);
    },

    init: function () {
        // Toggle password visibility
        const togglePassword = document.getElementById('togglePassword');
        if (togglePassword) {
            togglePassword.addEventListener('click', () => this.togglePassword('password'));
        }

        const toggleConfirmPassword = document.getElementById('toggleConfirmPassword');
        if (toggleConfirmPassword) {
            toggleConfirmPassword.addEventListener('click', () => this.togglePassword('confirmPassword'));
        }

        // Password strength checker
        const passwordField = document.getElementById('password');
        if (passwordField) {
            passwordField.addEventListener('input', (e) => {
                this.checkPasswordStrength(e.target.value);
            });
        }

        // Email validation on blur
        const emailField = document.getElementById('email');
        if (emailField) {
            emailField.addEventListener('blur', function () {
                const error = document.getElementById('emailError');
                if (this.value && !registerApp.isValidEmail(this.value)) {
                    error.textContent = 'Please enter a valid email address';
                    this.classList.add('is-invalid');
                } else {
                    error.textContent = '';
                    this.classList.remove('is-invalid');
                }
            });
        }

        // Confirm password validation
        const confirmPasswordField = document.getElementById('confirmPassword');
        if (confirmPasswordField) {
            confirmPasswordField.addEventListener('input', function () {
                const password = document.getElementById('password').value;
                const error = document.getElementById('confirmPasswordError');
                if (this.value && password !== this.value) {
                    error.textContent = 'Passwords do not match';
                    this.classList.add('is-invalid');
                } else {
                    error.textContent = '';
                    this.classList.remove('is-invalid');
                }
            });
        }

        // Form submission
        const form = document.getElementById('registerForm');
        if (form) {
            form.addEventListener('submit', async function (e) {
                e.preventDefault();

                // Clear previous errors
                document.querySelectorAll('.invalid-feedback').forEach(el => el.textContent = '');
                document.querySelectorAll('.form-control').forEach(el => el.classList.remove('is-invalid'));
                document.getElementById('errorAlert').classList.add('d-none');
                document.getElementById('successAlert').classList.add('d-none');

                const firstName = document.getElementById('firstName').value.trim();
                const lastName = document.getElementById('lastName').value.trim();
                const email = document.getElementById('email').value.trim();
                const password = document.getElementById('password').value;
                const confirmPassword = document.getElementById('confirmPassword').value;
                const agreeTerms = document.getElementById('agreeTerms').checked;

                let hasErrors = false;

                if (firstName.length < 2) {
                    document.getElementById('firstNameError').textContent = 'First name must be at least 2 characters';
                    document.getElementById('firstName').classList.add('is-invalid');
                    hasErrors = true;
                }

                if (lastName.length < 2) {
                    document.getElementById('lastNameError').textContent = 'Last name must be at least 2 characters';
                    document.getElementById('lastName').classList.add('is-invalid');
                    hasErrors = true;
                }

                if (!registerApp.isValidEmail(email)) {
                    document.getElementById('emailError').textContent = 'Please enter a valid email address';
                    document.getElementById('email').classList.add('is-invalid');
                    hasErrors = true;
                }

                if (password.length < 6) {
                    document.getElementById('passwordError').textContent = 'Password must be at least 6 characters';
                    document.getElementById('password').classList.add('is-invalid');
                    hasErrors = true;
                }

                if (password !== confirmPassword) {
                    document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
                    document.getElementById('confirmPassword').classList.add('is-invalid');
                    hasErrors = true;
                }

                if (!agreeTerms) {
                    document.getElementById('agreeTermsError').textContent = 'You must agree to the terms';
                    hasErrors = true;
                }

                if (hasErrors) return;

                document.getElementById('registerBtn').classList.add('d-none');
                document.getElementById('registerLoader').classList.remove('d-none');
                document.getElementById('submitBtn').disabled = true;

                try {
                    const response = await fetch('/api/account/register', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            firstName: firstName,
                            lastName: lastName,
                            email: email,
                            password: password,
                            confirmPassword: confirmPassword
                        })
                    });

                    const data = await response.json();

                    if (data.success) {
                        localStorage.setItem('token', data.token);
                        localStorage.setItem('user', JSON.stringify(data.user));

                        document.getElementById('successMessage').textContent = 'Account created successfully! Redirecting...';
                        document.getElementById('successAlert').classList.remove('d-none');

                        // Update progress
                        document.querySelectorAll('.progress-step').forEach(step => step.classList.add('active'));

                        setTimeout(() => {
                            window.location.href = '/account/dashboard';
                        }, 2000);
                    } else {
                        document.getElementById('errorMessage').textContent = data.message || 'Registration failed. Please try again.';
                        document.getElementById('errorAlert').classList.remove('d-none');
                    }
                } catch (error) {
                    console.error('Error:', error);
                    document.getElementById('errorMessage').textContent = 'An error occurred. Please try again.';
                    document.getElementById('errorAlert').classList.remove('d-none');
                } finally {
                    document.getElementById('registerBtn').classList.remove('d-none');
                    document.getElementById('registerLoader').classList.add('d-none');
                    document.getElementById('submitBtn').disabled = false;
                }
            });
        }
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    registerApp.init();
});