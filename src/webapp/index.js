import { BlobServiceClient } from "@azure/storage-blob";
const baguetteBox = require('baguettebox.js');

// Storage account

const sasBaseUrl = 'https://mecozzidemo.blob.core.windows.net/?sv=2020-08-04&ss=b&srt=sco&sp=rwdlacitfx&se=2022-03-31T12:55:19Z&st=2022-03-24T04:55:19Z&spr=https&sig=';
let sasSig = window.prompt('Please provide access key.');
console.log('Storage account connection string: ' + sasBaseUrl + sasSig);

const blobServiceClient = new BlobServiceClient(sasBaseUrl + sasSig);
const imageContainerClient = blobServiceClient.getContainerClient('describe-images');
const thumbnailContainerClient = blobServiceClient.getContainerClient('describe-images-thumbnails');

// Handling files

let uploadsInProgress = 0;
let uploadsDone = 0;
let uploadProgressBar = document.getElementById('upload-progress-bar');

function resetUploadProgress(filesCount) {
    uploadProgressBar.value = 0;
    uploadsDone = 0;
    uploadsInProgress = filesCount;
    document.getElementById('upload-error').innerText = '';
}

function uploadProgressDone(id) {
    uploadsDone++;
    uploadProgressBar.value = uploadsDone / uploadsInProgress * 100;
    setTimeout(() => {
        document.getElementById(id).outerHTML = '';
        loadGallery();
    }, 5000);
}

function uploadFailed() {
    document.getElementById('upload-error').innerText = 'Upload failed.';
}

function handleFiles(files) {
    files = [...files];
    resetUploadProgress(files.length);
    files.forEach(file => {
        const uniqueName = Date.now() + file.name;
        previewFile(uniqueName, file);
        uploadFile(uniqueName, file);
    });
}

function uploadFile(uniqueName, file) {
    console.log('Uploading: ' + file.name);
    const blockBlobClient = imageContainerClient.getBlockBlobClient(uniqueName);
    blockBlobClient.uploadData(file)
        .then(uploadProgressDone(uniqueName))
        .catch(uploadFailed);
}

function previewFile(uniqueName, file) {
    let img = document.createElement('img');
    img.id = uniqueName;
    img.src = URL.createObjectURL(file);
    document.getElementById('upload-gallery').appendChild(img);
}

// Displaying images

async function retrieveThumbnails() {
    const maxImages = 100;
    const thumbnailContainerUrl = new URL(thumbnailContainerClient.url);
    thumbnailContainerUrl.search = '';
    let thumbnails = [];
    try {
        for await (const blob of thumbnailContainerClient.listBlobsFlat({ includeMetadata: true })) {
            if (blob !== undefined && blob !== null) {
                const imageBlob = await imageContainerClient.getBlockBlobClient(blob.name)?.getProperties();
                const tags = imageBlob?.metadata?.tags;
                const description = tags?.includes('car')
                    ? decodeURIComponent(imageBlob.metadata.caption) 
                    : 'This image does not clearly feature a car.';
                thumbnails.push({
                    name: blob.name,
                    url: `${new URL(thumbnailContainerUrl)}/${blob.name}`,
                    thumbnailOf: blob.metadata.thumbnailof,
                    description: description
                });
            }
            if (thumbnails.length >= maxImages) break;
        }
        return thumbnails;
    } catch (error) {
        console.log(error);
        document.getElementById('download-error').innerText = 'Failure during download of image thumbnails.';
    }
}

async function loadGallery() {
    const gallery = document.getElementById('download-gallery');
    if (gallery.getAttribute('loading') == 'true') return;
    gallery.setAttribute('loading', 'true');
    gallery.innerHTML = '';
    const thumbnails = await retrieveThumbnails();
    thumbnails.forEach(thumbnail => {
        let link = document.createElement('a');
        link.href = thumbnail.thumbnailOf;
        link.title = thumbnail.description;
        let img = document.createElement('img');
        img.src = thumbnail.url;
        img.alt = thumbnail.name;
        link.appendChild(img);
        gallery.appendChild(link);
    });
    gallery.setAttribute('loading', 'false');

    baguetteBox.run('#download-gallery');
}

(async () => await loadGallery())();

// drop-area events

let dropAreaInput = document.getElementById('fileElem');
dropAreaInput.addEventListener('change', event => {
    let files = event.target.files;
    handleFiles(files);
}, false);

let dropArea = document.getElementById('drop-area');

['dragenter', 'dragleave', 'dragover', 'drop'].forEach(eventName => {
    dropArea.addEventListener(eventName, event => {
        event.preventDefault();
        event.stopPropagation();
    }, false);
});

['dragenter', 'dragover'].forEach(eventName => {
    dropArea.addEventListener(eventName, () => {
        dropArea.classList.add('highlight');
    }, false);
});

['dragleave', 'drop'].forEach(eventName => {
    dropArea.addEventListener(eventName, () => {
        dropArea.classList.remove('highlight');
    }, false);
});

dropArea.addEventListener('drop', event => {
    let files = event.dataTransfer.files;
    handleFiles(files);
}, false);