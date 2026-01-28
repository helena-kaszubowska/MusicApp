import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import UserLogin from '../../MusicAppFrontend/src/pages/UserLogin';
import UserRegister from '../../MusicAppFrontend/src/pages/UserRegister';
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

  // Test 9: Validates that registration form shows error when passwords do not match
  it('shows error when passwords do not match', async () => {
    const user = userEvent.setup();
    renderWithRouter(<UserRegister />);

    await user.type(screen.getByLabelText(/email/i), 'test@example.com');
    await user.type(screen.getByLabelText(/^password$/i), 'password123');
    await user.type(screen.getByLabelText(/confirm password/i), 'different123');
    await user.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() => {
      expect(screen.getByText(/passwords don't match/i)).toBeInTheDocument();
    });
  });

  // Test 10: Validates that registration form shows error when password is too short
  it('shows error when password is too short', async () => {
    const user = userEvent.setup();
    renderWithRouter(<UserRegister />);

    await user.type(screen.getByLabelText(/email/i), 'test@example.com');
    await user.type(screen.getByLabelText(/^password$/i), 'short');  // 5 znaków, wymagane min 8
    await user.type(screen.getByLabelText(/confirm password/i), 'short');
    await user.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() => {
      expect(screen.getByText(/password must be at least 8 characters/i)).toBeInTheDocument();
    });
  });
});
