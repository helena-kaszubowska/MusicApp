import React from "react";
import { IoMdCloseCircle } from "react-icons/io";
import toast from "react-hot-toast";
import { albumService } from "../../services/albumService";
import { motion, AnimatePresence } from "motion/react";
import Backdrop from "../common/Backdrop";

const dropIn = {
  initial: { y: -100, opacity: 0 },
  visible: { y: 0, opacity: 1 },
  exit: { y: 100, opacity: 0 }
};

export const AddAlbumForm = ({
  isModalOpen,
  setIsModalOpen,
  albumData,
  setAlbumData,
  onSuccess,
}) => {
  const handleAlbumChange = (e) => {
    const { name, value } = e.target;
    setAlbumData((prev) => ({ ...prev, [name]: value }));
  };

  const handleTrackChange = (index, e) => {
    const { name, value } = e.target;
    const updatedTracks = [...albumData.tracks];
    updatedTracks[index][name] = value;
    setAlbumData({ ...albumData, tracks: updatedTracks });
  };

  const handleAddTrack = () => {
    setAlbumData({
      ...albumData,
      tracks: [
        ...albumData.tracks,
        { title: "", artist: "", year: "", length: "", genre: "", nr: "" },
      ],
    });
  };

  const handleRemoveTrack = (index) => {
    const updatedTracks = [...albumData.tracks];
    updatedTracks.splice(index, 1);
    setAlbumData({ ...albumData, tracks: updatedTracks });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!albumData.title || !albumData.artist || albumData.tracks.length < 1) {
      toast.error("Album must have a title, an artist, and at least one track.");
      return;
    }

    try {
      await albumService.createAlbum(albumData);
      toast.success("Album added successfully!");
      setIsModalOpen(false);
      onSuccess();
    } catch (error) {
      toast.error("Error adding album.");
      console.error(error);
    }
  };

  if (!isModalOpen) return null;

  return (
    <AnimatePresence>
      {isModalOpen && (
        <Backdrop onClose={() => setIsModalOpen(false)} key="add-album-backdrop">
          <motion.div
            className="bg-white rounded-xl p-6 pr-0 w-full max-w-2xl relative"
            variants={dropIn}
            initial="initial"
            animate="visible"
            exit="exit"
            transition={{
              y: { type: "spring", bounce: 0.2 },
              duration: 0.3
            }}
          >
            <button
              className="absolute top-2 right-2 text-gray-500 hover:text-red-500 text-xl"
              onClick={() => setIsModalOpen(false)}
            >
              <IoMdCloseCircle className="w-7 h-7" />
            </button>
            <h2 className="text-lg font-semibold mb-4 text-gray-800">Add new album</h2>

            <form onSubmit={handleSubmit} className="space-y-4 max-h-[calc(100vh-10rem)] pr-6 overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                {["title", "artist", "year", "label", "coverUrl"].map((field, idx) => (
                  <input
                    key={field}
                    type={field === "year" ? "number" : "text"}
                    name={field}
                    placeholder={field.charAt(0).toUpperCase() + field.slice(1)}
                    value={albumData[field]}
                    onChange={handleAlbumChange}
                    required={["title", "artist", "year"].includes(field)}
                    className={`border px-3 py-2 rounded ${field === "coverUrl" ? "col-span-2" : ""}`}
                  />
                ))}
              </div>

              <div className="mt-6">
                <h3 className="font-semibold text-gray-700 mb-2">Tracks</h3>
                {albumData.tracks.map((track, index) => (
                  <div key={index} className="grid grid-cols-6 gap-2 mb-2 items-center">
                    {["title", "artist", "length", "genre", "year", "nr"].map((field) => (
                      <input
                        key={field}
                        type={["length", "year", "nr"].includes(field) ? "number" : "text"}
                        name={field}
                        placeholder={field}
                        value={track[field]}
                        onChange={(e) => handleTrackChange(index, e)}
                        className="border px-2 py-1 rounded"
                        required={["title", "length"].includes(field)}
                      />
                    ))}
                    <button
                      type="button"
                      onClick={() => handleRemoveTrack(index)}
                      className="col-span-2 bg-red-500 px-2 py-1 text-white hover:bg-red-700 rounded"
                    >
                      Remove
                    </button>
                  </div>
                ))}
                <button type="button" onClick={handleAddTrack} className="mt-2 text-blue-500 hover:underline text-sm">
                  + Add another track
                </button>
              </div>

              <button type="submit" className="mt-4 bg-purple-600 text-white px-4 py-2 rounded hover:bg-purple-700 transition">
                Add album
              </button>
            </form>
          </motion.div>
        </Backdrop>
        )}
    </AnimatePresence>
  );
};

export const EditAlbumForm = ({
  editData,
  setEditData,
  handleEditTrackChange,
  handleAddEditTrack,
  handleRemoveEditTrack,
  handleEditSubmit,
  setIsEditModalOpen,
}) => {
  const handleEditAlbumChange = (e) => {
    const { name, value } = e.target;
    setEditData({ ...editData, [name]: value });
  };

  return (
    <Backdrop onClose={() => setIsEditModalOpen(false)}>
      <motion.div
        className="bg-white rounded-xl p-6 pr-0 w-full max-w-2xl relative"
        variants={dropIn}
        initial="initial"
        animate="visible"
        exit="exit"
        transition={{
          y: { type: "spring", bounce: 0.2 },
          duration: 0.3
        }}
      >
      <button
        className="absolute top-2 right-2 text-gray-500 hover:text-red-500 text-xl"
        onClick={() => setIsEditModalOpen(false)}
      >
        <IoMdCloseCircle className="w-7 h-7" />
      </button>
      <h2 className="text-lg font-semibold mb-4 text-gray-800">Edit album</h2>
        <form onSubmit={handleEditSubmit} className="space-y-4 max-h-[calc(100vh-10rem)] pr-6 overflow-y-auto">
          <div className="grid grid-cols-2 gap-4">
            {["title", "artist", "year", "label", "coverUrl"].map((field) => (
              <input
                key={field}
                type={field === "year" ? "number" : "text"}
                name={field}
                placeholder={field}
                value={editData[field]}
                onChange={handleEditAlbumChange}
                required={["title", "artist", "year"].includes(field)}
                className={`border px-3 py-2 rounded ${field === "coverUrl" ? "col-span-2" : ""}`}
              />
            ))}
          </div>

          <div className="mt-6">
            <h3 className="font-semibold text-gray-700 mb-2">Tracks</h3>
            {editData.tracks.map((track, index) => (
              <div key={index} className="grid grid-cols-6 gap-2 mb-2 items-center">
                {["title", "artist", "length", "genre", "year", "nr"].map((field) => (
                  <input
                    key={field}
                    type={["length", "year", "nr"].includes(field) ? "number" : "text"}
                    name={field}
                    placeholder={field}
                    value={track[field]}
                    onChange={(e) => handleEditTrackChange(index, e)}
                    className="border px-2 py-1 rounded"
                    required={["title", "length"].includes(field)}
                  />
                ))}
                <button
                  type="button"
                  onClick={() => handleRemoveEditTrack(index)}
                  className="col-span-2 bg-red-500 px-2 py-1 text-white hover:bg-red-700 rounded"
                >
                  Remove
                </button>
              </div>
            ))}
            <button type="button" onClick={handleAddEditTrack} className="mt-2 text-blue-500 hover:underline text-sm">
              + Add another track
            </button>
          </div>

          <button type="submit" className="mt-4 bg-purple-600 text-white px-4 py-2 rounded hover:bg-purple-700 transition">
            Save changes
          </button>
        </form>
      </motion.div>
    </Backdrop>
  );
};
