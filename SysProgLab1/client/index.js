const button = document.createElement('button')
button.innerHTML = 'fetch'
document.body.appendChild(button)
button.onclick = () => {
    fetch("http://localhost:8080/file1.txt")
    .then(response => response.text())
    .then(data => {
        console.log(data);
    })
}

