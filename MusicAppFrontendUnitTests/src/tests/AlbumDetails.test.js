import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import AlbumDetails from '../../MusicAppFrontend/src/pages/AlbumDetails';
import { albumService } from '../../MusicAppFrontend/src/services/albumService';
import { useAuth } from '../../MusicAppFrontend/src/hooks/AuthContext';
import { useLibrary } from '../../MusicAppFrontend/src/hooks/LibraryContext';

jest.mock('../../MusicAppFrontend/src/services/albumService');
jest.mock('../../MusicAppFrontend/src/hooks/AuthContext');
jest.mock('../../MusicAppFrontend/src/hooks/LibraryContext');
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useParams: () => ({ albumId: '123' }),
  useNavigate: () => jest.fn(),
}));

const mockAlbum = {
  id: '123',
  _id: '123',
  title: 'Test Album',
  artist: 'Test Artist',
  coverUrl: 'https://example.com/cover.jpg',
  tracks: [
    { id: 't1', _id: 't1', title: 'Track 1', length: 180 },
    { id: 't2', _id: 't2', title: 'Track 2', length: 200 },
    { id: 't3', _id: 't3', title: 'Track 3', length: 240 },
  ],
};

describe('AlbumDetails', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAuth.mockReturnValue({
      isLoggedIn: true,
      userEmail: 'test@example.com',
    });
    useLibrary.mockReturnValue({
      isAlbumInLibrary: jest.fn(() => false),
      isTrackInLibrary: jest.fn(() => false),
      addAlbumToLibrary: jest.fn(),
      addTrackToLibrary: jest.fn(),
    });
  });

  // Test 1: Verifies that album details page displays track list with track titles, durations, and "Add to Library" buttons
  it('renders tracklist', async () => {
    albumService.getAlbumById.mockResolvedValue(mockAlbum);
    render(<AlbumDetails />);

    await waitFor(() => {
      expect(screen.getByText('Track 1')).toBeInTheDocument();
    });
    
    expect(screen.getByText('Track 2')).toBeInTheDocument();
    expect(screen.getByText(/3:00/i)).toBeInTheDocument();
    expect(screen.getAllByText(/add to library/i).length).toBeGreaterThanOrEqual(3);
  });

  // Test 2: Verifies that loading skeleton is displayed while API request is in progress
  it('shows loading skeleton', () => {
    albumService.getAlbumById.mockImplementation(() => new Promise(() => {}));
    const { container } = render(<AlbumDetails />);

    // Verify that skeleton loading elements are present (elements with animate-pulse class)
    const skeletonElements = container.querySelectorAll('.animate-pulse');
    expect(skeletonElements.length).toBeGreaterThan(0);
    
    // Verify that actual album data is not displayed
    expect(screen.queryByText('Test Album')).not.toBeInTheDocument();
    expect(screen.queryByText('Track 1')).not.toBeInTheDocument();
  });
});
