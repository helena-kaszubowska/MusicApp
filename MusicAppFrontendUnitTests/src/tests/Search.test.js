import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Search from '../../MusicAppFrontend/src/pages/Search';
import { albumService } from '../../MusicAppFrontend/src/services/albumService';

jest.mock('../../MusicAppFrontend/src/services/albumService');
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useSearchParams: () => [new URLSearchParams(), jest.fn()],
}));

describe('Search', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  // Test 6: Verifies that search function is called with debounce delay of 150ms after user stops typing
  it('calls search after 150ms debounce', async () => {
    const mockSearch = jest.fn().mockResolvedValue([]);
    albumService.searchAlbums = mockSearch;

    const user = userEvent.setup({ delay: null });
    render(<Search />);

    await user.type(screen.getByPlaceholderText(/search albums/i), 'test');
    expect(mockSearch).not.toHaveBeenCalled();

    jest.advanceTimersByTime(150);

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith('test');
    });
  });
});
