// Register Form JavaScript
const registerApp = {
    togglePassword: function(fieldId) {
        const field = document.getElementById(fieldId);
        const buttons = document.querySelectorAll('.toggle-password');
        const button = event.target.closest('.toggle-password');
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

    checkPasswordStrength: function(password) {
        const bars = document.querySelectorAll('#passwordStrength .strength-bar');
        const strengthText = document.getElementById('strengthText');
        let strength = 0;

        if (!password) {
            bars.forEach(bar => {
                bar.classList.remove('weak', 'fair', 'good', 'strong');
            });
            strengthText.textContent = '';
            return;
        }

        if (password.length >= 6) strength++;
        if (password.length >= 10) strength++;
        if (password.length >= 14) strength++;
        if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[^a-zA-Z0-9]/.test(password)) strength++;

        const barCount = strength > 4 ? 4 : strength;
        const strengthLevels = ['weak', 'fair', 'good', 'strong'];
        const strengthLabels = ['Weak', 'Fair', 'Good', 'Strong'];

        bars.forEach((bar, index) => {
            bar.classList.remove('weak', 'fair', 'good', 'strong');
            if (index < barCount) {
                bar.classList.add(strengthLevels[barCount - 1]);
            }
        });

        strengthText.textContent = strengthLabels[barCount - 1];
    },

    init: function() {
        document.getElementById('password').addEventListener('input', function() {
            registerApp.checkPasswordStrength(this.value);
        });

        document.getElementById('registerForm').addEventListener('submit', async function(e) {
            e.preventDefault();

            document.querySelectorAll('.invalid-feedback').forEach(el => el.textContent = '');
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
                hasErrors = true;
            }

            if (lastName.length < 2) {
                document.getElementById('lastNameError').textContent = 'Last name must be at least 2 characters';
                hasErrors = true;
            }

            if (!registerApp.isValidEmail(email)) {
                document.getElementById('emailError').textContent = 'Please enter a valid email address';
                hasErrors = true;
            }

            if (password.length < 6) {
                document.getElementById('passwordError').textContent = 'Password must be at least 6 characters';
                hasErrors = true;
            }

            if (password !== confirmPassword) {
                document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
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

        document.getElementById('email').addEventListener('blur', function() {
            if (this.value && !registerApp.isValidEmail(this.value)) {
                document.getElementById('emailError').textContent = 'Please enter a valid email address';
            } else {
                document.getElementById('emailError').textContent = '';
            }
        });

        document.getElementById('confirmPassword').addEventListener('input', function() {
            const password = document.getElementById('password').value;
            if (this.value && password !== this.value) {
                document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
            } else {
                document.getElementById('confirmPasswordError').textContent = '';
            }
        });
    },

    isValidEmail: function(email) {
        var pattern = /^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
        return pattern.test(email);
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    registerApp.init();
});// Register Form JavaScript
const registerApp = {
    togglePassword: function(fieldId) {
        const field = document.getElementById(fieldId);
        const buttons = document.querySelectorAll('.toggle-password');
        const button = event.target.closest('.toggle-password');
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

    checkPasswordStrength: function(password) {
        const bars = document.querySelectorAll('#passwordStrength .strength-bar');
        const strengthText = document.getElementById('strengthText');
        let strength = 0;

        if (!password) {
            bars.forEach(bar => {
                bar.classList.remove('weak', 'fair', 'good', 'strong');
            });
            strengthText.textContent = '';
            return;
        }

        if (password.length >= 6) strength++;
        if (password.length >= 10) strength++;
        if (password.length >= 14) strength++;
        if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[^a-zA-Z0-9]/.test(password)) strength++;

        const barCount = strength > 4 ? 4 : strength;
        const strengthLevels = ['weak', 'fair', 'good', 'strong'];
        const strengthLabels = ['Weak', 'Fair', 'Good', 'Strong'];

        bars.forEach((bar, index) => {
            bar.classList.remove('weak', 'fair', 'good', 'strong');
            if (index < barCount) {
                bar.classList.add(strengthLevels[barCount - 1]);
            }
        });

        strengthText.textContent = strengthLabels[barCount - 1];
    },

    init: function() {
        document.getElementById('password').addEventListener('input', function() {
            registerApp.checkPasswordStrength(this.value);
        });

        document.getElementById('registerForm').addEventListener('submit', async function(e) {
            e.preventDefault();

            document.querySelectorAll('.invalid-feedback').forEach(el => el.textContent = '');
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
                hasErrors = true;
            }

            if (lastName.length < 2) {
                document.getElementById('lastNameError').textContent = 'Last name must be at least 2 characters';
                hasErrors = true;
            }

            if (!registerApp.isValidEmail(email)) {
                document.getElementById('emailError').textContent = 'Please enter a valid email address';
                hasErrors = true;
            }

            if (password.length < 6) {
                document.getElementById('passwordError').textContent = 'Password must be at least 6 characters';
                hasErrors = true;
            }

            if (password !== confirmPassword) {
                document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
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

        document.getElementById('email').addEventListener('blur', function() {
            if (this.value && !registerApp.isValidEmail(this.value)) {
                document.getElementById('emailError').textContent = 'Please enter a valid email address';
            } else {
                document.getElementById('emailError').textContent = '';
            }
        });

        document.getElementById('confirmPassword').addEventListener('input', function() {
            const password = document.getElementById('password').value;
            if (this.value && password !== this.value) {
                document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
            } else {
                document.getElementById('confirmPasswordError').textContent = '';
            }
        });
    },

    isValidEmail: function(email) {
        var pattern = /^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
        return pattern.test(email);
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    registerApp.init();
});// Register Form JavaScript
const registerApp = {
    togglePassword: function (fieldId) {
        const field = document.getElementById(fieldId);
        const buttons = document.querySelectorAll('.toggle-password');
        const button = event.target.closest('.toggle-password');
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
            return;
        }

        if (password.length >= 6) strength++;
        if (password.length >= 10) strength++;
        if (password.length >= 14) strength++;
        if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[^a-zA-Z0-9]/.test(password)) strength++;

        const barCount = strength > 4 ? 4 : strength;
        const strengthLevels = ['weak', 'fair', 'good', 'strong'];
        const strengthLabels = ['Weak', 'Fair', 'Good', 'Strong'];

        bars.forEach((bar, index) => {
            bar.classList.remove('weak', 'fair', 'good', 'strong');
            if (index < barCount) {
                bar.classList.add(strengthLevels[barCount - 1]);
            }
        });

        strengthText.textContent = strengthLabels[barCount - 1];
    },

    init: function () {
        document.getElementById('password').addEventListener('input', function () {
            registerApp.checkPasswordStrength(this.value);
        });

        document.getElementById('registerForm').addEventListener('submit', async function (e) {
            e.preventDefault();

            document.querySelectorAll('.invalid-feedback').forEach(el => el.textContent = '');
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
                hasErrors = true;
            }

            if (lastName.length < 2) {
                document.getElementById('lastNameError').textContent = 'Last name must be at least 2 characters';
                hasErrors = true;
            }

            if (!registerApp.isValidEmail(email)) {
                document.getElementById('emailError').textContent = 'Please enter a valid email address';
                hasErrors = true;
            }

            if (password.length < 6) {
                document.getElementById('passwordError').textContent = 'Password must be at least 6 characters';
                hasErrors = true;
            }

            if (password !== confirmPassword) {
                document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
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

        document.getElementById('email').addEventListener('blur', function () {
            if (this.value && !registerApp.isValidEmail(this.value)) {
                document.getElementById('emailError').textContent = 'Please enter a valid email address';
            } else {
                document.getElementById('emailError').textContent = '';
            }
        });

        document.getElementById('confirmPassword').addEventListener('input', function () {
            const password = document.getElementById('password').value;
            if (this.value && password !== this.value) {
                document.getElementById('confirmPasswordError').textContent = 'Passwords do not match';
            } else {
                document.getElementById('confirmPasswordError').textContent = '';
            }
        });
    },

    isValidEmail: function (email) {
        var pattern = /^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
        return pattern.test(email);
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    registerApp.init();
});