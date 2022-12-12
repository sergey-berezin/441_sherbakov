var swaggerUrl = "http://localhost:5041/swagger/v1/swagger.json"
var photosArray = []
$(async () => 
{
    try {
        var swaggerClient = await SwaggerClient(swaggerUrl)
        let loadButton = document.getElementById("buttonLoad")
        loadButton.swagger = swaggerClient
        loadButton.addEventListener('click', LoadDataDB)
        let uploadAFile = document.getElementById("uploadFile")
        uploadAFile.swagger = swaggerClient
        uploadAFile.onchange = async e => {await uploadFiles(e)}
        let buttonUpload = document.getElementById('buttonUpload')
        buttonUpload.addEventListener('click', () => {uploadAFile.click()})
        let buttonDelete = document.getElementById('buttonDelete')
        buttonDelete.swagger = swaggerClient
        buttonDelete.addEventListener('click', deleteFromDB)
        let btnClear = document.getElementById('buttonClear')
        btnClear.addEventListener('click', clearAll)
        let select = document.getElementById('selectEmotion')
        select.addEventListener('change', photosSort)
    } 
    catch (error) {
        console.log(`error in init: ${error}`)
    }
})
async function LoadDataDB (event) {
    try {
        document.getElementById('progressButton').value = 0
        toggleButtons()
        if (event == undefined) {
            var client = await SwaggerClient(swaggerUrl)
        }
        else {
            client = event.currentTarget.swagger
        }
        let photos = await client.apis.Photos.Photos_GetPhotos()
        let photoDiv = document.getElementsByClassName('photos__body')[0]
        photoDiv.innerHTML = ''
        photosArray = []
    
        photos.body.forEach(item => {
            let blob = item.details.imageBLOB
            photosArray.push(
                {
                    'fileName': item.fileName,
                    'img': blob,
                    'emotions': item.emotions,
                    'optionEmotion': 'Option'
                }
            )
        })
        photosSort()
    }
    catch (error) {
        console.log(`error in LoadDB: ${error}`)
    }
    finally {
        toggleButtons()
    }
}

async function bindPhotos(event) {
    clearAllBesidesPhotoArray()
    let photoDiv = document.getElementsByClassName('photos__body')[0]
    let len = photosArray.length
    let bar = document.getElementById('progressButton')
    bar.setAttribute('max', len)
    photosArray.forEach(photo => {
        let blob = photo.img
        let wrapperDiv = document.createElement('div')
        wrapperDiv.setAttribute('class', 'photos__bodyWrapper')
        wrapperDiv.setAttribute('tabindex', '0')
        wrapperDiv.emotions = photo.emotions
        wrapperDiv.optionEmotion = photo.optionEmotion
        wrapperDiv.addEventListener('click', showEmotions)
        let rootDiv = document.createElement('div')
        rootDiv.setAttribute('class', 'photo__item')
        let rootDivInnerHtml = 
        `
            <span> ${photo.fileName} </span>
            <img src='data:image/png;base64,${blob}', alt=''/>
        `
        rootDiv.innerHTML = rootDivInnerHtml
        wrapperDiv.appendChild(rootDiv)
        photoDiv.appendChild(wrapperDiv)
        bar.value++
    })
}

async function showEmotions(event) {
    try {
        let divEmo = document.getElementsByClassName('emotions__body')[0]
        divEmo.innerHTML = ''
        divEmo.insertAdjacentHTML('beforeend', `<div class = 'emotions__item'> ${event.currentTarget.optionEmotion} </div>`)
        event.currentTarget.emotions.forEach(item => {
            let st = `${item.emoName}: ${item.emoOdds.toLocaleString(undefined, { maximumFractionDigits: 5, minimumFractionDigits: 5})}`
            divEmo.insertAdjacentHTML('beforeend', `<div class = 'emotions__item'> ${st} </div>`)
        })
    }
    catch (error) {
        console.log(`error in showEmotions: ${error}`)
    }
}

async function photosSort(event) {
    let val = document.getElementById('selectEmotion').value
    let arr = photosArray.sort((photo1, photo2) => 
        {
            let emo1 = photo1.emotions.filter(emotion => emotion.emoName == val)[0]
            let emo2 = photo2.emotions.filter(emotion => emotion.emoName == val)[0]
            if (emo1.emoOdds < emo2.emoOdds) {
                return 1
            }
            return -1
        })
    photosArray.forEach(photo => {
        let emotion = photo.emotions.filter(emotion => emotion.emoName == val)[0]
        photo.optionEmotion = `${emotion.emoName}: ${emotion.emoOdds.toLocaleString(undefined, { maximumFractionDigits: 5, minimumFractionDigits: 5})}`
    });
    photosArray = arr
    bindPhotos()
}

async function focusedPhoto(event) {
    console.log(event)
}

function readBLOB(file) {
    return new Promise((res, rej) => {
        const reader = new FileReader()
        reader.onloadend = e => res(e.target.result)
        reader.onerror = e => rej(e)
        reader.readAsDataURL(file)
    })
}

const uploadFiles = async (event) => {
    try 
    {
        toggleButtons()
        if (event == undefined) {
            var client = await SwaggerClient(swaggerUrl)
        }
        else {
            client = event.currentTarget.swagger
        } 
        let files = event.target.files
        let PromisesArr = []
        let bar = document.getElementById('progressButton')
        bar.value = 0
        bar.setAttribute('max', files.length)
        for (let file of files) {
            PromisesArr.push(new Promise(async res => {
                let blob = await readBLOB(file)
                let title = file.name
                let postObj = {
                    'img': blob.split(',')[1], 
                    'fname': title,
                }
                let postResponse = await client.apis.Photos.Photos_PostImage({obj: postObj})
                let id = postResponse.obj.id
                let getResponse = await client.apis.Photos.Photos_GetPhoto({id: id})
                let photo = getResponse.body
            
                photosArray.push(
                    {
                        'fileName': photo.fileName,
                        'img': blob.split(',')[1],
                        'emotions': photo.emotions,
                        'optionEmotion': 'Option'
                    }
                )
                bar.value++
                res(true)
            }))
        }
        await Promise.all(PromisesArr).then(
            () => {bar.value = files.length; toggleButtons(); photosSort()}
        )
    }
    catch (error) {
        console.log(`error in uploadFiles: ${error}`)
    }
    finally {
        // finally doesn't always work for async functions :(
    }
}
async function clearAll(event) {
    try {
        document.getElementById('progressButton').value = 0
        photosArray = []
        let emotionsDiv = document.getElementsByClassName('emotions__body')[0]
        emotionsDiv.innerHTML = ''
        let photosDiv = document.getElementsByClassName('photos__body')[0]
        photosDiv.innerHTML = ''
    }
    catch (error) {
        console.log(`error in clearAll: ${error}`)
    }
}
async function clearAllBesidesPhotoArray(event) {
    try {
        let emotionsDiv = document.getElementsByClassName('emotions__body')[0]
        emotionsDiv.innerHTML = ''
        let photosDiv = document.getElementsByClassName('photos__body')[0]
        photosDiv.innerHTML = ''
    }
    catch (error) {
        console.log(`error in clearAllBesidesPhotoArray: ${error}`)
    }
}
async function deleteFromDB(event) {
    try {
        let bar = document.getElementById('progressButton')
        bar.value = 0
        bar.setAttribute('max', 1)
        toggleButtons()
        if (event == undefined) {
            var client = await SwaggerClient(swaggerUrl)
        }
        else {
            client = event.currentTarget.swagger
        }
        client.apis.Photos.Photos_DeletePhotos()
        bar.value = 1
        clearAll()
    }
    catch (error) {
        console.log(`error in deleteFromDB: ${error}`)
    }
    finally {
        toggleButtons()
    }
}

document.addEventListener('keyup', function(event) {
    if (event.code == 'KeyC') {
        let elem = document.getElementById('buttonClear')
        if (elem.classList.contains('linkDisabled')) return
        setButtonStyle(elem)
        elem.click()
    }
    else if ((event.code == 'KeyL'))  {
        let elem = document.getElementById('buttonLoad')
        if (elem.classList.contains('linkDisabled')) return
        setButtonStyle(elem)
        elem.click()
    }
    else if ((event.code == 'KeyD'))  {
        let elem = document.getElementById('buttonDelete')
        if (elem.classList.contains('linkDisabled')) return
        setButtonStyle(elem)
        elem.click()
    }
    else if ((event.code == 'KeyP'))  {
        let elem = document.getElementById('buttonUpload')
        if (elem.classList.contains('linkDisabled')) return
        setButtonStyle(elem)
        elem.click()
    }
});

function setButtonStyle (elem) {
    elem.classList.toggle("clicked");
    elem.classList.toggle("clicking");
    setTimeout(function(){ 
        elem.classList.toggle("clicked");
        elem.classList.toggle("clicking");
    },140);
}

function toggleButtons() {
    let first = document.getElementById('buttonLoad')
    let second = document.getElementById('buttonUpload')
    let third = document.getElementById('buttonDelete')
    let fourth = document.getElementById('buttonClear')
    first.classList.toggle('linkDisabled')
    second.classList.toggle('linkDisabled')
    third.classList.toggle('linkDisabled')
    fourth.classList.toggle('linkDisabled')
}