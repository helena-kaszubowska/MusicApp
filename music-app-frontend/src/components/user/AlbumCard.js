import React, { useState, useMemo } from "react";
import ReactDOM from "react-dom";
import { useNavigate } from "react-router-dom";
import { FaPlay } from "react-icons/fa";
import { IoAddCircleOutline, IoCheckmarkCircle } from "react-icons/io5";
import { IoMdTrash } from "react-icons/io";
import { IoIosAdd } from "react-icons/io";
import { IoMdCloseCircle } from "react-icons/io";
import { IoPencil } from "react-icons/io5";
import toast from "react-hot-toast";
import { useAuth } from "../../hooks/AuthContext";
import { useLibrary } from "../../hooks/LibraryContext";
import { albumService } from "../../services/albumService";
import { AddAlbumForm, EditAlbumForm } from "./AlbumForms";
import { AnimatePresence } from "motion/react";

const AlbumCard = ({ album }) => {
  const navigate = useNavigate();
  const { isLoggedIn, isAdmin } = useAuth();
  const { isAlbumInLibrary, addAlbumToLibrary } = useLibrary();
  const [isAdding, setIsAdding] = useState(false);
  const [isAdded, setIsAdded] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editData, setEditData] = useState({ ...album });

  // useMemo - sprawdza czy album jest już w bibliotece
  const isInLibrary = useMemo(() => {
    return isAlbumInLibrary(album._id || album.id);
  }, [isAlbumInLibrary, album]);

  const handleCardClick = () => {
    navigate(`/album/${album._id || album.id}`);
  };

  const handleAddToLibrary = async (e) => {
    e.stopPropagation();

    if (!isLoggedIn) {
      navigate("/login");
      return;
    }

    if (isInLibrary) {
      toast.error("This album is already in your library!", {
        duration: 3000,
        position: "bottom-center",
        style: {
          background: "#ef4444",
          color: "#fff",
        },
      });
      return;
    }

    try {
      setIsAdding(true);
      await addAlbumToLibrary(album._id || album.id);
      setIsAdded(true);

      toast.success("Album added to library!", {
        duration: 2000,
        position: "bottom-center",
        style: {
          background: "#10b981",
          color: "#fff",
        },
      });

      setTimeout(() => {
        setIsAdded(false);
      }, 2000);
    } catch (error) {
      console.error("Failed to add album to library:", error);
      toast.error("Failed to add album to library");

      if (
        error.message?.includes("401") ||
        error.message?.includes("Unauthorized")
      ) {
        navigate("/login");
      }
    } finally {
      setIsAdding(false);
    }
  };

  const handleDelete = async (e) => {
    e.stopPropagation();

    const confirm = window.confirm("Are you sure you want to delete this album?");
    if (!confirm) return;

    try {
      await albumService.deleteAlbum(album._id || album.id);

      toast.success("Album deleted!", {
        duration: 3000,
        position: "bottom-center",
        style: {
          background: "#ef4444",
          color: "#fff",
        },
      });
      window.location.reload();

    } catch (error) {
      toast.error("Failed to delete the album");
      console.error("Deletion failed:", error);
      window.location.reload();
    }
  };

  const handleEditClick = async (e) => {
    e.stopPropagation();
    const albumDataToEdit = await albumService.getAlbumById(album.id);
    setEditData(albumDataToEdit);
    setIsEditModalOpen(true);
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    try {
      await albumService.updateAlbum(editData._id || editData.id, editData);
      toast.success("Album updated successfully!");
      setIsEditModalOpen(false);
      window.location.reload();
    } catch (error) {
      console.error("Update failed:", error);
      toast.error("Album update hasn't succeed.");
    }
  };

  const handleEditTrackChange = (index, e) => {
    const { name, value } = e.target;
    const updatedTracks = [...editData.tracks];
    updatedTracks[index][name] = value;
    setEditData({ ...editData, tracks: updatedTracks });
  };

  const handleAddEditTrack = () => {
    setEditData({
      ...editData,
      tracks: [
        ...(Array.isArray(editData.tracks) ? editData.tracks : []),
        {
          title: "",
          artist: "",
          year: "",
          length: "",
          genre: "",
          nr: "",
        },
      ],
    });
  };

  const handleRemoveEditTrack = (index) => {
    const updatedTracks = [...editData.tracks];
    updatedTracks.splice(index, 1);
    setEditData({ ...editData, tracks: updatedTracks });
  };

  return (
    <div
      className="bg-black/40 backdrop-blur-sm rounded-lg border border-purple-500/20 cursor-pointer group hover:bg-black/60 transition-all duration-300 relative w-full flex flex-col"
      onClick={handleCardClick}
    >
      {isLoggedIn && isAdmin && (
        <div className="absolute top-2 right-2 flex flex-row-reverse gap-1 opacity-0 group-hover:opacity-100 transition-opacity duration-200 z-10">
          <button
            className="text-gray-400 hover:text-red-500 transition-colors"
            onClick={handleDelete}
          >
            <IoMdTrash className="w-5 h-5" />
          </button>
          <button
            className="text-gray-400 hover:text-white transition-colors"
            onClick={(e) => {
              e.stopPropagation();
              handleEditClick(e);
            }}
          >
            <IoPencil className="w-5 h-5 pointer-events-none" />
          </button>
        </div>
      )}
      <div className="relative flex-1 overflow-hidden rounded-t-lg">
        <img
          src={album.coverUrl || album.coverImage}
          alt={album.title}
          className="w-full h-full object-cover"
        />
        <div className="absolute inset-0 bg-black bg-opacity-0 group-hover:bg-opacity-30 transition-all duration-300 flex items-center justify-center opacity-0 group-hover:opacity-100">
          <button className="bg-purple-600 hover:bg-purple-700 rounded-full p-3 text-white transition-colors">
            <FaPlay className="w-5 h-5" />
          </button>
        </div>
      </div>
      <div className="flex justify-between items-center px-3 py-2 min-h-[60px] bg-black/40 relative rounded-b-lg">
        <div className="flex-1 min-w-0">
          <h3 className="text-sm font-semibold text-white truncate">
            {album.title}
          </h3>
          <p className="text-xs text-gray-300 truncate">{album.artist}</p>
        </div>
        <div className="relative group/button ml-2 overflow-visible">
          {isInLibrary ? (
            <div className="text-green-400">
              <IoCheckmarkCircle className="w-5 h-5" />
            </div>
          ) : (
            <button
              onClick={handleAddToLibrary}
              disabled={isAdding}
              className={`transition-all duration-300 hover:scale-110 ${isAdded ? "text-green-400" : "text-purple-300 hover:text-white"
                }`}
            >
              {isAdded ? (
                <IoCheckmarkCircle className="w-5 h-5 animate-bounce" />
              ) : isAdding ? (
                <div className="w-5 h-5 border-2 border-purple-300 border-t-transparent rounded-full animate-spin"></div>
              ) : (
                <IoAddCircleOutline className="w-5 h-5" />
              )}
            </button>
          )}
          <span
            className="absolute px-3 py-2 bg-gray-900 border border-purple-500/30 text-white text-xs rounded opacity-0 group-hover/button:opacity-100 transition-opacity duration-200 whitespace-nowrap shadow-xl pointer-events-none"
            style={{
              zIndex: 999999,
              bottom: "100%",
              right: "0",
              marginBottom: "8px",
            }}
          >
            {isInLibrary
              ? "Already in Library"
              : isAdded
                ? "Added!"
                : isAdding
                  ? "Adding..."
                  : "Add to Library"}
          </span>
        </div>
      </div>
      {ReactDOM.createPortal(
        <AnimatePresence>
          {isEditModalOpen &&
            <EditAlbumForm
              editData={editData}
              setEditData={setEditData}
              handleEditTrackChange={handleEditTrackChange}
              handleAddEditTrack={handleAddEditTrack}
              handleRemoveEditTrack={handleRemoveEditTrack}
              handleEditSubmit={handleEditSubmit}
              setIsEditModalOpen={setIsEditModalOpen}
            />}
        </AnimatePresence>,
        document.body
      )}
    </div>
  );
};

export default AlbumCard;

export const AddAlbumCard = () => {
  const { isLoggedIn, isAdmin } = useAuth();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [albumData, setAlbumData] = useState({
    title: "",
    artist: "",
    year: "",
    label: "",
    coverUrl: "",
    tracks: [
      { title: "", artist: "", year: "", length: "", genre: "", nr: "" },
    ],
  });

  if (!isLoggedIn || !isAdmin) return null;

  return (
    <>
      <button
        onClick={() => setIsModalOpen(true)}
        className="cursor-pointer bg-black/20 border-2 border-dashed border-gray-400 rounded-lg flex items-center justify-center text-gray-400 hover:bg-gray-700 transition-colors duration-200"
      >
        <IoIosAdd className="w-12 h-12" />
      </button>

      <AddAlbumForm
        isModalOpen={isModalOpen}
        setIsModalOpen={setIsModalOpen}
        albumData={albumData}
        setAlbumData={setAlbumData}
        onSuccess={() => {
          setAlbumData({
            title: "",
            artist: "",
            year: "",
            label: "",
            coverUrl: "",
            tracks: [
              { title: "", artist: "", year: "", length: "", genre: "", nr: "" },
            ],
          });
          window.location.reload();
        }}
      />
    </>
  );
};
