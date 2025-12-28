// Login Form JavaScript
const loginApp = {
    init: function () {
        document.getElementById('loginForm').addEventListener('submit', async function (e) {
            e.preventDefault();

            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            const rememberMe = document.getElementById('rememberMe').checked;

            document.getElementById('emailError').textContent = '';
            document.getElementById('passwordError').textContent = '';
            document.getElementById('errorAlert').classList.add('d-none');
            document.getElementById('successAlert').classList.add('d-none');

            document.getElementById('loginBtn').classList.add('d-none');
            document.getElementById('loginLoader').classList.remove('d-none');

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

                    document.getElementById('successAlert').textContent = 'Login successful! Redirecting...';
                    document.getElementById('successAlert').classList.remove('d-none');

                    setTimeout(() => {
                        window.location.href = '/account/dashboard';
                    }, 1500);
                } else {
                    document.getElementById('errorAlert').textContent = data.message || 'Login failed. Please try again.';
                    document.getElementById('errorAlert').classList.remove('d-none');
                }
            } catch (error) {
                document.getElementById('errorAlert').textContent = 'An error occurred. Please try again.';
                document.getElementById('errorAlert').classList.remove('d-none');
            } finally {
                document.getElementById('loginBtn').classList.remove('d-none');
                document.getElementById('loginLoader').classList.add('d-none');
            }
        });
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    loginApp.init();
});