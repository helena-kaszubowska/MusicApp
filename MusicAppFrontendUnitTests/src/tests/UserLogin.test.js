import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import UserLogin from '../../MusicAppFrontend/src/pages/UserLogin';
import { useAuth } from '../../MusicAppFrontend/src/hooks/AuthContext';

jest.mock('../../MusicAppFrontend/src/hooks/AuthContext');
const mockNavigate = jest.fn();
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useNavigate: () => mockNavigate,
}));

const mockLogin = jest.fn();

beforeEach(() => {
  jest.clearAllMocks();
  global.fetch = jest.fn();
  mockNavigate.mockClear();
  useAuth.mockReturnValue({
    isLoggedIn: false,
    login: mockLogin,
    loading: false,
  });
});

const renderWithRouter = (component) => {
  return render(<MemoryRouter>{component}</MemoryRouter>);
};

// Helper function to fill and submit login form
const fillAndSubmitForm = async (email = 'test@example.com', password = 'password123') => {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText(/email/i), email);
  await user.type(screen.getByLabelText(/password/i), password);
  await user.click(screen.getByRole('button', { name: /sign in/i }));
};

describe('UserLogin', () => {
  // Test 7: Verifies that login form renders all required input fields and submit button
  it('renders form fields', () => {
    renderWithRouter(<UserLogin />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByText(/sign up here/i)).toBeInTheDocument();
  });

  // Test 8: Validates that form shows error message when email field is empty on submit
  it('shows error when email is empty', async () => {
    const user = userEvent.setup();
    renderWithRouter(<UserLogin />);

    await user.type(screen.getByLabelText(/password/i), 'password123');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByText(/email is required/i)).toBeInTheDocument();
    });
  });

  // Test 9: Verifies successful login flow - API call, token storage via login function, and navigation to home page
  it('saves token and redirects on successful login', async () => {
    const mockUserData = {
      token: 'mock-token-123',
      email: 'test@example.com',
      roles: ['user'],
    };

    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: true,
        json: async () => mockUserData,
      })
    );

    renderWithRouter(<UserLogin />);
    await fillAndSubmitForm();

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalled();
    });
    
    expect(mockLogin).toHaveBeenCalledWith(mockUserData);
    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  // Test 10: Ensures error message is displayed and user stays on login page when API returns 401 Unauthorized
  it('shows error message on 401', async () => {
    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: false,
        status: 401,
        json: async () => ({ message: 'Invalid email or password' }),
      })
    );

    renderWithRouter(<UserLogin />);
    await fillAndSubmitForm('test@example.com', 'wrongpassword');

    await waitFor(() => {
      expect(screen.getByText(/invalid email or password/i)).toBeInTheDocument();
    });
    
    expect(mockNavigate).not.toHaveBeenCalled();
  });
});
