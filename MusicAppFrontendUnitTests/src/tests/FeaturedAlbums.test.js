import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import FeaturedAlbums from '../../MusicAppFrontend/src/pages/FeaturedAlbums';
import { albumService } from '../../MusicAppFrontend/src/services/albumService';

jest.mock('../../MusicAppFrontend/src/services/albumService');
jest.mock('../../MusicAppFrontend/src/components/user/AlbumCard', () => {
  const MockAlbumCard = ({ album }) => (
    <div>
      <h3>{album.title}</h3>
      <p>{album.artist}</p>
      <img src={album.coverUrl || album.coverImage} alt={album.title} />
    </div>
  );
  
  const MockAddAlbumCard = () => <div>Add Album Card</div>;
  
  return {
    __esModule: true,
    default: MockAlbumCard,
    AddAlbumCard: MockAddAlbumCard,
  };
});

const mockAlbums = [
  { id: '1', _id: '1', title: 'Test Album 1', artist: 'Test Artist 1', coverUrl: 'https://example.com/cover1.jpg' },
  { id: '2', _id: '2', title: 'Test Album 2', artist: 'Test Artist 2', coverUrl: 'https://example.com/cover2.jpg' },
];

describe('FeaturedAlbums', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  // Test 3: Verifies that album list displays album titles, artist names, and cover images correctly
  it('renders album data', async () => {
    albumService.getAllAlbums.mockResolvedValue(mockAlbums);
    render(<FeaturedAlbums />);

    await waitFor(() => {
      expect(screen.getByText('Test Album 1')).toBeInTheDocument();
    });
    
    expect(screen.getByText('Test Artist 1')).toBeInTheDocument();
    expect(screen.getByAltText('Test Album 1')).toHaveAttribute('src', 'https://example.com/cover1.jpg');
  });

  // Test 4: Checks that album data is not displayed while API request is still in progress
  it('shows loading state', () => {
    albumService.getAllAlbums.mockImplementation(() => new Promise(() => {}));
    render(<FeaturedAlbums />);

    expect(screen.queryByText('Test Album 1')).not.toBeInTheDocument();
  });

  // Test 5: Ensures no albums are rendered when API returns an empty array
  it('does not show albums when list is empty', async () => {
    albumService.getAllAlbums.mockResolvedValue([]);
    render(<FeaturedAlbums />);

    await waitFor(() => {
      expect(screen.queryByText('Test Album 1')).not.toBeInTheDocument();
    });
  });
});
