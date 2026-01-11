const API_BASE_URL =
  process.env.REACT_APP_API_BASE_URL ||
  "https://bbz40djmz4.execute-api.eu-north-1.amazonaws.com/default/api";

export const API_ENDPOINTS = {
  albums: {
    getAll: `${API_BASE_URL}/albums`,
    getById: (id) => `${API_BASE_URL}/albums/${id}`,
    search: (query) =>
      `${API_BASE_URL}/albums/search?query=${encodeURIComponent(query)}`,
    create: `${API_BASE_URL}/albums`,
    update: (id) => `${API_BASE_URL}/albums/${id}`,
    delete: (id) => `${API_BASE_URL}/albums/${id}`,
  },
  auth: {
    login: `${API_BASE_URL}/sign-in`,
    register: `${API_BASE_URL}/sign-up`,
  },
  library: {
    getUserLibrary: `${API_BASE_URL}/Library/album`,
    addToLibrary: `${API_BASE_URL}/Library/albums`,
    removeFromLibrary: `${API_BASE_URL}/Library/albums`,
    getUserTracks: `${API_BASE_URL}/Library/tracks`,
    addTrack: `${API_BASE_URL}/Library/tracks`,
    removeTrack: `${API_BASE_URL}/Library/tracks`,
  },
  admin: {
    giveAdminRights: `${API_BASE_URL}/User/give-admin-rights`,
  },
};

export const apiRequest = async (url, options = {}) => {
  const token = localStorage.getItem("authToken");

  const config = {
    headers: {
      "Content-Type": "application/json",
      ...(token && { Authorization: `Bearer ${token}` }),
      ...options.headers,
    },
    ...options,
  };

  try {
    const response = await fetch(url, config);

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    // funkcja sprawdzająca czy odpowiedź jest formacie json, tak- parsuje, nie- zwraca status o sukcesie
    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("application/json")) {
      return await response.json();
    }

    return { success: true, status: response.status };
  } catch (error) {
    console.error("API request failed:", error);
    throw error;
  }
};

export default API_BASE_URL;
