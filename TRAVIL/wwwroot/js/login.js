// Login Form JavaScript - TRAVEL
const loginApp = {
    togglePassword: function () {
        const field = document.getElementById('password');
        const button = document.getElementById('togglePassword');
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

    init: function () {
        // Toggle password visibility
        const toggleBtn = document.getElementById('togglePassword');
        if (toggleBtn) {
            toggleBtn.addEventListener('click', this.togglePassword);
        }

        // Form submission
        const form = document.getElementById('loginForm');
        if (form) {
            form.addEventListener('submit', async function (e) {
                e.preventDefault();

                const email = document.getElementById('email').value;
                const password = document.getElementById('password').value;
                const rememberMe = document.getElementById('rememberMe').checked;

                document.getElementById('errorAlert').classList.add('d-none');
                document.getElementById('successAlert').classList.add('d-none');

                document.getElementById('loginBtn').classList.add('d-none');
                document.getElementById('loginLoader').classList.remove('d-none');
                document.getElementById('submitBtn').disabled = true;

                try {
                    const response = await fetch('/api/account/login', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            email: email,
                            password: password,
                            rememberMe: rememberMe
                        })
                    });

                    const data = await response.json();

                    if (data.success) {
                        localStorage.setItem('token', data.token);
                        localStorage.setItem('user', JSON.stringify(data.user));

                        document.getElementById('successMessage').textContent = 'Login successful! Redirecting...';
                        document.getElementById('successAlert').classList.remove('d-none');

                        setTimeout(() => {
                            window.location.href = '/account/dashboard';
                        }, 1500);
                    } else {
                        document.getElementById('errorMessage').textContent = data.message || 'Login failed. Please try again.';
                        document.getElementById('errorAlert').classList.remove('d-none');
                    }
                } catch (error) {
                    document.getElementById('errorMessage').textContent = 'An error occurred. Please try again.';
                    document.getElementById('errorAlert').classList.remove('d-none');
                } finally {
                    document.getElementById('loginBtn').classList.remove('d-none');
                    document.getElementById('loginLoader').classList.add('d-none');
                    document.getElementById('submitBtn').disabled = false;
                }
            });
        }
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    loginApp.init();
});